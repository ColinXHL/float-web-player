// main.js - 原神方向标记插件（字幕版）v2.0.0
// 适配 Plugin API v2 - 使用扁平化全局对象

/**
 * 从文本中提取方向信息
 * 支持：东/西/南/北、东北/东南/西北/西南
 * 支持：小地图右上/右下/左上/左下/右/左/上/下 + 后缀（边/方/手边/侧）
 * 支持：各种后缀（方向、边、方、侧、手边）
 * 支持：钟点方向（3点钟方向、12点方向等）
 */
function extractDirections(text) {
    var results = [];
    
    // 方向词匹配 - 注意：复合方向（东北等）必须放在单字方向（东等）前面，确保优先匹配
    // 第一部分：东北/东南/西北/西南/东/西/南/北 + 可选后缀（方向/边/方/侧）
    // 第二部分：地图 + 右上/右下/左上/左下/右/左/上/下 + 必须有后缀（边/方/手边/侧），避免匹配"地图上角"
    var directionRegex = /(东北|东南|西北|西南|东|西|南|北)(?:方向|边|方|侧)?|(?:小?)地图(?:的)?\s*(右上|右下|左上|左下|右|左|上|下)(边|方|手边|侧)/g;
    
    // 钟点方向匹配（1-12点钟方向）
    var clockRegex = /(\d{1,2})\s*点(?:钟)?(?:方向)?/g;
    
    var directionMapping = {
        '东': 'east',
        '西': 'west',
        '南': 'south',
        '北': 'north',
        '东北': 'northeast',
        '东南': 'southeast',
        '西北': 'northwest',
        '西南': 'southwest',
        '右上': 'northeast',
        '右下': 'southeast',
        '左上': 'northwest',
        '左下': 'southwest',
        '右': 'east',
        '左': 'west',
        '上': 'north',
        '下': 'south'
    };
    
    // 钟点到方向的映射（以12点为北）
    var clockMapping = {
        12: 'north',
        1: 'northeast',
        2: 'northeast',
        3: 'east',
        4: 'southeast',
        5: 'southeast',
        6: 'south',
        7: 'southwest',
        8: 'southwest',
        9: 'west',
        10: 'northwest',
        11: 'northwest'
    };
    
    var match;
    
    // 匹配方向词
    while ((match = directionRegex.exec(text)) !== null) {
        var word;
        if (match[1]) {
            word = match[1];
        } else if (match[2]) {
            word = match[2];
        }
        
        if (word && directionMapping[word]) {
            results.push(directionMapping[word]);
        }
    }
    
    // 匹配钟点方向
    while ((match = clockRegex.exec(text)) !== null) {
        var hour = parseInt(match[1], 10);
        if (hour >= 1 && hour <= 12 && clockMapping[hour]) {
            results.push(clockMapping[hour]);
        }
    }
    
    return results;
}

/**
 * 插件加载时调用
 * Plugin API v2: 不再需要 api 参数，直接使用全局对象
 */
function onLoad() {
    log.info(plugin.name + " v" + plugin.version + " 已加载（字幕版）");
    
    // 检查字幕 API 是否可用
    if (!subtitle) {
        log.warn("警告：字幕 API 不可用，请检查插件权限");
        return;
    }
    
    // 从配置读取覆盖层位置（原神小地图默认位置）
    // Plugin API v2: 使用 config.get() 或 settings.xxx
    var x = config.get("overlay.x", 43);
    var y = config.get("overlay.y", 43);
    var size = config.get("overlay.size", 212);
    var duration = config.get("markerDuration", 0);

    log.info("覆盖层位置和大小: x=" + x + ", y=" + y + ", size=" + size);
    log.debug("duration 值: " + duration + ", 类型: " + typeof duration + ", === 0: " + (duration === 0) + ", == 0: " + (duration == 0));

    // 从配置读取标记样式
    var markerSize = config.get("marker.size", 32);
    var markerImage = config.get("marker.image", "assets/right.png");

    log.debug("markerImage 配置: " + markerImage + " | " + typeof markerImage);
    
    // 设置覆盖层
    log.info("设置覆盖层位置和大小...");
    overlay.setPosition(x, y);
    overlay.setSize(size, size);
    
    // 只有常驻模式（duration == 0）才在启动时显示覆盖层
    // 非常驻模式等待第一次方向词匹配时再显示
    var isPermanentMode = (duration == 0 || duration === null || duration === undefined);
    log.debug("isPermanentMode: " + isPermanentMode);
    if (isPermanentMode) {
        overlay.show();
    }
    log.info("覆盖层设置完成");
    
    // 应用标记样式
    log.debug("应用标记样式, size=" + markerSize);
    try {
        overlay.setMarkerStyle({ size: markerSize });
        log.debug("标记样式设置完成");
    } catch (e) {
        log.error("设置标记样式失败: " + e.message);
    }
    
    // 调试：检查 overlay 对象的方法
    log.debug("检查 overlay 对象...");
    log.debug("overlay.setMarkerImage 类型: " + typeof overlay.setMarkerImage);
    log.debug("overlay.SetMarkerImage 类型: " + typeof overlay.SetMarkerImage);
    
    // 设置标记图片（图片应指向右/东方向）
    if (markerImage) {
        log.debug("准备设置标记图片: " + markerImage);
        try {
            // 尝试小写方法名（ClearScript 的 camelCase 转换）
            if (typeof overlay.setMarkerImage === 'function') {
                var result = overlay.setMarkerImage(markerImage);
                log.debug("setMarkerImage 结果: " + result);
            } 
            // 尝试原始方法名
            else if (typeof overlay.SetMarkerImage === 'function') {
                var result = overlay.SetMarkerImage(markerImage);
                log.debug("SetMarkerImage 结果: " + result);
            } else {
                log.error("错误: setMarkerImage 方法不存在！");
            }
        } catch (e) {
            log.error("设置标记图片失败: " + e.message + " | " + e.toString());
        }
    } else {
        log.debug("markerImage 配置为空，跳过设置");
    }
    
    // 如果 duration 为 0（常驻模式），初始化时显示北方向标记，让用户知道遮罩层已生效
    // 如果设置了显示时长，则不显示初始标记，等待字幕中出现方向词时再显示
    if (isPermanentMode) {
        log.info("常驻模式：显示初始北方向标记");
        overlay.showMarker("north", 0);
    } else {
        log.info("非常驻模式：不显示初始标记，等待方向词匹配");
    }
    
    // 字幕加载时，预处理并统计方向信息
    subtitle.on("load", function(subtitleData) {
        var directionCount = 0;
        
        subtitleData.body.forEach(function(entry) {
            var directions = extractDirections(entry.content);
            if (directions.length > 0) {
                directionCount++;
            }
        });
        
        log.info("字幕已加载，共 " + subtitleData.body.length + " 条字幕，其中 " + directionCount + " 条包含方向信息");
    });
    
    // 监听字幕变化，实时显示方向标记
    subtitle.on("change", function(sub) {
        if (sub) {
            var directions = extractDirections(sub.content);
            
            if (directions.length > 0) {
                log.info("识别到方向: " + directions.join(", ") + " (字幕: " + sub.content + ")");
                
                // 显示最后一个方向（通常是最新提到的）
                var lastDirection = directions[directions.length - 1];
                overlay.showMarker(lastDirection, duration);
            }
        }
    });
    
    // 字幕清除时的处理
    subtitle.on("clear", function() {
        log.info("字幕已清除");
    });
}

/**
 * 插件卸载时调用
 * Plugin API v2: 不再需要 api 参数，直接使用全局对象
 */
function onUnload() {
    log.info("原神方向标记插件已卸载");
    subtitle.off("load");
    subtitle.off("change");
    subtitle.off("clear");
    overlay.hide();
}

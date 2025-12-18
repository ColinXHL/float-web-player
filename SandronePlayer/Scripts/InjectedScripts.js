/**
 * FloatWebPlayer - WebView2 注入脚本
 * 
 * 此文件作为嵌入资源，通过 AddScriptToExecuteOnDocumentCreatedAsync 注入。
 * 样式由 InjectedStyles.css 提供，本文件仅负责 DOM 操作和事件处理。
 */
(function () {
    'use strict';

    console.log('[SandronePlayer] Script loaded');

    // 防止重复注入
    if (window.__sandronePlayerInjected) {
        console.log('[SandronePlayer] Already injected, skipping');
        return;
    }
    window.__sandronePlayerInjected = true;
    console.log('[SandronePlayer] Starting injection');

    // ========================================
    // DOM 元素创建
    // ========================================

    /**
     * 创建拖动区域
     */
    function createDragZone() {
        // 防止重复创建
        if (document.getElementById('float-player-drag-zone')) return;

        const dragZone = document.createElement('div');
        dragZone.id = 'float-player-drag-zone';

        dragZone.addEventListener('mousedown', function (e) {
            e.preventDefault();
            e.stopPropagation();
            window.chrome.webview.postMessage('drag');
        });

        document.body.appendChild(dragZone);
    }

    /**
     * 创建控制按钮
     */
    function createControlButtons() {
        // 防止重复创建
        if (document.getElementById('float-player-controls')) return null;

        const controls = document.createElement('div');
        controls.id = 'float-player-controls';

        // 最小化按钮
        const minimizeBtn = document.createElement('button');
        minimizeBtn.title = '最小化';
        minimizeBtn.textContent = '−';
        minimizeBtn.addEventListener('click', () => {
            window.chrome.webview.postMessage('minimize');
        });

        // 最大化/还原按钮
        const maximizeBtn = document.createElement('button');
        maximizeBtn.title = '最大化/还原';
        maximizeBtn.textContent = '□';
        maximizeBtn.addEventListener('click', () => {
            window.chrome.webview.postMessage('maximize');
        });

        // 关闭按钮
        const closeBtn = document.createElement('button');
        closeBtn.className = 'close';
        closeBtn.title = '关闭';
        closeBtn.textContent = '×';
        closeBtn.addEventListener('click', () => {
            window.chrome.webview.postMessage('close');
        });

        controls.appendChild(minimizeBtn);
        controls.appendChild(maximizeBtn);
        controls.appendChild(closeBtn);

        document.body.appendChild(controls);
        return controls;
    }

    // ========================================
    // 事件处理
    // ========================================

    /**
     * 设置控制按钮的显示/隐藏逻辑
     */
    function setupVisibilityHandlers(controls) {
        if (!controls) return;

        // 鼠标进入文档时显示按钮
        document.addEventListener('mouseenter', function () {
            controls.classList.add('visible');
        });

        // 鼠标离开文档时隐藏按钮
        document.addEventListener('mouseleave', function () {
            controls.classList.remove('visible');
        });

        // 鼠标进入控制按钮区域时保持显示
        controls.addEventListener('mouseenter', function () {
            controls.classList.add('visible');
        });
    }

    /**
     * 处理全屏状态变化
     * 当元素进入全屏时，将控制按钮移动到全屏元素内部
     */
    function handleFullscreenChange() {
        const controls = document.getElementById('float-player-controls');
        const dragZone = document.getElementById('float-player-drag-zone');
        
        if (!controls || !dragZone) return;

        // 获取当前全屏元素（兼容不同浏览器）
        const fullscreenElement = document.fullscreenElement || 
                                  document.webkitFullscreenElement || 
                                  document.msFullscreenElement;

        if (fullscreenElement) {
            // 将控制元素移动到全屏元素内
            console.log('[SandronePlayer] Moving controls to fullscreen element');
            fullscreenElement.appendChild(dragZone);
            fullscreenElement.appendChild(controls);
        } else {
            // 退出全屏，移回 body
            console.log('[SandronePlayer] Moving controls back to body');
            document.body.appendChild(dragZone);
            document.body.appendChild(controls);
        }
    }

    /**
     * 设置全屏状态监听
     */
    function setupFullscreenHandler() {
        // 监听全屏状态变化（兼容不同浏览器）
        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('msfullscreenchange', handleFullscreenChange);
    }

    // ========================================
    // 初始化
    // ========================================

    function initialize() {
        console.log('[SandronePlayer] initialize() called, document.body:', !!document.body, 'readyState:', document.readyState);
        
        // 等待 body 可用（AddScriptToExecuteOnDocumentCreatedAsync 可能在 body 创建前执行）
        if (!document.body) {
            // 使用多种方式确保初始化
            if (document.readyState === 'loading') {
                console.log('[SandronePlayer] Waiting for DOMContentLoaded');
                document.addEventListener('DOMContentLoaded', initialize);
            } else {
                // 短暂延迟后重试
                console.log('[SandronePlayer] Retrying in 10ms');
                setTimeout(initialize, 10);
            }
            return;
        }

        console.log('[SandronePlayer] Creating elements...');
        createDragZone();
        const controls = createControlButtons();
        setupVisibilityHandlers(controls);
        setupFullscreenHandler();
        console.log('[SandronePlayer] Injection complete!');
    }

    // 执行初始化
    initialize();
})();

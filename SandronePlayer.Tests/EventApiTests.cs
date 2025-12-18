using System;
using System.Collections.Generic;
using System.IO;
using SandronePlayer.Models;
using SandronePlayer.Plugins;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SandronePlayer.Tests
{
    /// <summary>
    /// EventApi 属性测试和单元测试
    /// </summary>
    public class EventApiTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PluginContext _context;
        private readonly EventApi _eventApi;

        public EventApiTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"event_api_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            var manifest = new PluginManifest
            {
                Id = "test-event-plugin",
                Name = "Test Event Plugin",
                Version = "1.0.0",
                Main = "main.js",
                Permissions = new List<string> { "events" }
            };

            File.WriteAllText(Path.Combine(_tempDir, "main.js"), "function onLoad() {} function onUnload() {}");

            _context = new PluginContext(manifest, _tempDir);
            _eventApi = new EventApi(_context);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }

        #region Property 9: 事件监听器管理

        /// <summary>
        /// **Feature: plugin-api-enhancement, Property 9: 事件监听器管理**
        /// *对于任意*事件名称和回调函数，调用 On 注册后触发事件应调用回调；
        /// 调用 Off 取消后触发事件不应调用回调。
        /// **Validates: Requirements 6.1, 6.2**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property EventListenerManagement_OnAndOff(PositiveInt eventIndex)
        {
            // 使用支持的事件名称
            var eventName = EventApi.SupportedEvents[eventIndex.Get % EventApi.SupportedEvents.Length];
            
            var callCount = 0;
            object? receivedData = null;
            Action<object> callback = (data) =>
            {
                callCount++;
                receivedData = data;
            };

            // 注册监听器
            _eventApi.On(eventName, callback);
            var listenerCountAfterOn = _eventApi.GetListenerCount(eventName);

            // 触发事件
            var testData = new { test = "value" };
            _eventApi.Emit(eventName, testData);
            var callCountAfterEmit = callCount;

            // 取消监听器
            _eventApi.Off(eventName, callback);
            var listenerCountAfterOff = _eventApi.GetListenerCount(eventName);

            // 再次触发事件
            callCount = 0;
            _eventApi.Emit(eventName, testData);
            var callCountAfterOffEmit = callCount;

            // 清理
            _eventApi.ClearAllListeners();

            var isCorrect = listenerCountAfterOn == 1 && 
                           callCountAfterEmit == 1 && 
                           listenerCountAfterOff == 0 && 
                           callCountAfterOffEmit == 0;

            return isCorrect.Label($"Event: {eventName}, ListenerAfterOn: {listenerCountAfterOn}, " +
                                   $"CallAfterEmit: {callCountAfterEmit}, ListenerAfterOff: {listenerCountAfterOff}, " +
                                   $"CallAfterOffEmit: {callCountAfterOffEmit}");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Property 9: 事件监听器管理（多监听器）**
        /// **Validates: Requirements 6.1, 6.2**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property EventListenerManagement_MultipleListeners(PositiveInt seed)
        {
            var eventName = "testEvent";
            var listenerCount = (seed.Get % 5) + 1; // 1-5 个监听器
            var callCounts = new int[listenerCount];
            var callbacks = new Action<object>[listenerCount];

            // 注册多个监听器
            for (int i = 0; i < listenerCount; i++)
            {
                var index = i;
                callbacks[i] = (data) => callCounts[index]++;
                _eventApi.On(eventName, callbacks[i]);
            }

            var registeredCount = _eventApi.GetListenerCount(eventName);

            // 触发事件
            _eventApi.Emit(eventName, new { });

            // 验证所有监听器都被调用
            var allCalled = callCounts.All(c => c == 1);
            var totalCalls = callCounts.Sum();

            // 清理
            _eventApi.ClearAllListeners();

            var isCorrect = registeredCount == listenerCount && allCalled && totalCalls == listenerCount;

            return isCorrect.Label($"ListenerCount: {listenerCount}, Registered: {registeredCount}, " +
                                   $"AllCalled: {allCalled}, TotalCalls: {totalCalls}");
        }

        /// <summary>
        /// **Feature: plugin-api-enhancement, Property 9: 事件监听器管理（Off 移除所有）**
        /// **Validates: Requirements 6.1, 6.2**
        /// </summary>
        [Property(MaxTest = 50)]
        public Property EventListenerManagement_OffRemovesAll(PositiveInt seed)
        {
            var eventName = "testRemoveAll";
            var listenerCount = (seed.Get % 5) + 2; // 2-6 个监听器
            var callCounts = new int[listenerCount];

            // 注册多个监听器
            for (int i = 0; i < listenerCount; i++)
            {
                var index = i;
                _eventApi.On(eventName, (data) => callCounts[index]++);
            }

            var countBeforeOff = _eventApi.GetListenerCount(eventName);

            // 使用 Off(eventName, null) 移除所有监听器
            _eventApi.Off(eventName, null);

            var countAfterOff = _eventApi.GetListenerCount(eventName);

            // 触发事件
            _eventApi.Emit(eventName, new { });

            // 验证没有监听器被调用
            var totalCalls = callCounts.Sum();

            // 清理
            _eventApi.ClearAllListeners();

            var isCorrect = countBeforeOff == listenerCount && countAfterOff == 0 && totalCalls == 0;

            return isCorrect.Label($"ListenerCount: {listenerCount}, BeforeOff: {countBeforeOff}, " +
                                   $"AfterOff: {countAfterOff}, TotalCalls: {totalCalls}");
        }

        #endregion

        #region Unit Tests

        /// <summary>
        /// On 注册后应该增加监听器计数
        /// </summary>
        [Fact]
        public void On_ShouldIncreaseListenerCount()
        {
            var eventName = "testEvent";
            Action<object> callback = (data) => { };

            Assert.Equal(0, _eventApi.GetListenerCount(eventName));

            _eventApi.On(eventName, callback);

            Assert.Equal(1, _eventApi.GetListenerCount(eventName));

            _eventApi.ClearAllListeners();
        }

        /// <summary>
        /// 重复注册相同回调不应该增加计数
        /// </summary>
        [Fact]
        public void On_DuplicateCallback_ShouldNotIncrease()
        {
            var eventName = "testEvent";
            Action<object> callback = (data) => { };

            _eventApi.On(eventName, callback);
            _eventApi.On(eventName, callback);

            Assert.Equal(1, _eventApi.GetListenerCount(eventName));

            _eventApi.ClearAllListeners();
        }

        /// <summary>
        /// Off 应该减少监听器计数
        /// </summary>
        [Fact]
        public void Off_ShouldDecreaseListenerCount()
        {
            var eventName = "testEvent";
            Action<object> callback = (data) => { };

            _eventApi.On(eventName, callback);
            Assert.Equal(1, _eventApi.GetListenerCount(eventName));

            _eventApi.Off(eventName, callback);
            Assert.Equal(0, _eventApi.GetListenerCount(eventName));
        }

        /// <summary>
        /// Emit 应该调用所有注册的回调
        /// </summary>
        [Fact]
        public void Emit_ShouldCallAllCallbacks()
        {
            var eventName = "testEvent";
            var callCount1 = 0;
            var callCount2 = 0;

            _eventApi.On(eventName, (data) => callCount1++);
            _eventApi.On(eventName, (data) => callCount2++);

            _eventApi.Emit(eventName, new { });

            Assert.Equal(1, callCount1);
            Assert.Equal(1, callCount2);

            _eventApi.ClearAllListeners();
        }

        /// <summary>
        /// Emit 应该传递正确的数据
        /// </summary>
        [Fact]
        public void Emit_ShouldPassCorrectData()
        {
            var eventName = "testEvent";
            object? receivedData = null;

            _eventApi.On(eventName, (data) => receivedData = data);

            var testData = new { value = 42, name = "test" };
            _eventApi.Emit(eventName, testData);

            Assert.Same(testData, receivedData);

            _eventApi.ClearAllListeners();
        }

        /// <summary>
        /// 回调异常不应该影响其他回调
        /// </summary>
        [Fact]
        public void Emit_CallbackException_ShouldNotAffectOthers()
        {
            var eventName = "testEvent";
            var callCount = 0;

            _eventApi.On(eventName, (data) => throw new Exception("Test exception"));
            _eventApi.On(eventName, (data) => callCount++);

            // 不应该抛出异常
            _eventApi.Emit(eventName, new { });

            // 第二个回调应该被调用
            Assert.Equal(1, callCount);

            _eventApi.ClearAllListeners();
        }

        /// <summary>
        /// ClearAllListeners 应该移除所有监听器
        /// </summary>
        [Fact]
        public void ClearAllListeners_ShouldRemoveAll()
        {
            _eventApi.On("event1", (data) => { });
            _eventApi.On("event2", (data) => { });
            _eventApi.On("event3", (data) => { });

            _eventApi.ClearAllListeners();

            Assert.Equal(0, _eventApi.GetListenerCount("event1"));
            Assert.Equal(0, _eventApi.GetListenerCount("event2"));
            Assert.Equal(0, _eventApi.GetListenerCount("event3"));
        }

        /// <summary>
        /// 事件名称应该不区分大小写
        /// </summary>
        [Fact]
        public void EventName_ShouldBeCaseInsensitive()
        {
            var callCount = 0;
            Action<object> callback = (data) => callCount++;

            _eventApi.On("TestEvent", callback);
            _eventApi.Emit("testevent", new { });

            Assert.Equal(1, callCount);

            _eventApi.ClearAllListeners();
        }

        /// <summary>
        /// 空事件名称应该被忽略
        /// </summary>
        [Fact]
        public void On_EmptyEventName_ShouldBeIgnored()
        {
            _eventApi.On("", (data) => { });
            _eventApi.On(null!, (data) => { });
            _eventApi.On("  ", (data) => { });

            // 不应该抛出异常，也不应该注册任何监听器
            Assert.Equal(0, _eventApi.GetListenerCount(""));
        }

        /// <summary>
        /// 空回调应该被忽略
        /// </summary>
        [Fact]
        public void On_NullCallback_ShouldBeIgnored()
        {
            _eventApi.On("testEvent", null!);

            Assert.Equal(0, _eventApi.GetListenerCount("testEvent"));
        }

        #endregion
    }
}

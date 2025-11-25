using Snet.Core.extend;
using Snet.Model.data;
using Snet.Model.@interface;
using Snet.Utility;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Snet.Redis.RedisData;
namespace Snet.Redis
{
    /// <summary>
    /// redis操作
    /// </summary>
    public class RedisOperate : CoreUnify<RedisOperate, Basics>, IOn, IOff, IGetStatus, IGetObject, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 有参构造函数
        /// </summary>
        /// <param name="basics">基础数据</param>
        public RedisOperate(Basics basics) : base(basics) { }

        /// <summary>
        /// 连接的Redis对象
        /// </summary>
        private IConnectionMultiplexer? conn;
        /// <summary>
        /// 数据库
        /// </summary>
        private IDatabase? db;
        /// <inheritdoc/>
        public override void Dispose()
        {
            Off(true);
            base.Dispose();
        }
        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            await OffAsync(true);
            await base.DisposeAsync();
        }
        /// <inheritdoc/>
        public OperateResult On()
        {
            BegOperate();
            try
            {
                if (GetStatus().GetDetails(out string? message))
                {
                    return EndOperate(false, message);
                }
                //连接
                conn = ConnectionMultiplexer.Connect(basics.ConnectStr);
                //选择数据库
                db = conn.GetDatabase(basics.DataBaseID);
                //添加注册事件
                AddRegisterEvent();
                return EndOperate(true);
            }
            catch (Exception ex)
            {
                Off(true);
                return EndOperate(false, ex.Message, exception: ex);
            }
        }
        /// <inheritdoc/>
        public OperateResult Off(bool HardClose = false)
        {
            BegOperate();
            try
            {
                if (!HardClose)
                {
                    if (!GetStatus().GetDetails(out string? message))
                    {
                        return EndOperate(false, message);
                    }
                }
                conn?.Close();
                conn?.Dispose();
                conn = null;
                db = null;
                return EndOperate(true);
            }
            catch (Exception ex)
            {
                return EndOperate(false, ex.Message, exception: ex);
            }
        }
        /// <inheritdoc/>
        public OperateResult GetStatus()
        {
            BegOperate();
            try
            {
                if (conn == null || !conn.IsConnected)
                {
                    return EndOperate(false, "未连接", logOutput: false);
                }
                return EndOperate(true, "已连接", logOutput: false);
            }
            catch (Exception ex)
            {
                return EndOperate(false, ex.Message, exception: ex);
            }
        }
        /// <inheritdoc/>
        public OperateResult GetBaseObject()
        {
            BegOperate();
            if (!GetStatus().GetDetails(out string? message))
            {
                return EndOperate(false, message);
            }
            return EndOperate(true, resultData: conn);
        }
        /// <inheritdoc/>
        public async Task<OperateResult> OnAsync(CancellationToken token = default) => await Task.Run(() => On(), token);
        /// <inheritdoc/>
        public async Task<OperateResult> OffAsync(bool hardClose = false, CancellationToken token = default) => await Task.Run(() => Off(hardClose), token);
        /// <inheritdoc/>
        public async Task<OperateResult> GetStatusAsync(CancellationToken token = default) => await Task.Run(() => GetStatus(), token);
        /// <inheritdoc/>
        public async Task<OperateResult> GetBaseObjectAsync(CancellationToken token = default) => await Task.Run(() => GetBaseObject(), token);

        #region 注册事件

        /// <summary>
        /// 添加注册事件
        /// </summary>
        private void AddRegisterEvent()
        {
            if (GetStatus().Status)
            {
                conn.ConnectionRestored += Conn_ConnectionRestored;
                conn.ConnectionFailed += Conn_ConnectionFailed;
                conn.ErrorMessage += Conn_ErrorMessage;
                conn.ConfigurationChanged += Conn_ConfigurationChanged;
                conn.HashSlotMoved += Conn_HashSlotMoved;
                conn.InternalError += Conn_InternalError;
                conn.ConfigurationChangedBroadcast += Conn_ConfigurationChangedBroadcast;
            }
        }
        /// <summary>
        /// 建立物理连接时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_ConnectionRestored(object? sender, ConnectionFailedEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_ConnectionRestored)}: {e.Exception}"));
        }

        /// <summary>
        /// 重新配置广播时（通常意味着主从同步更改）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_ConfigurationChangedBroadcast(object? sender, EndPointEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_ConfigurationChangedBroadcast)}: {e.EndPoint}"));
        }

        /// <summary>
        /// 发生内部错误时（主要用于调试）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_InternalError(object? sender, InternalErrorEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_InternalError)}: {e.Exception}"));
        }

        /// <summary>
        /// 更改集群时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_HashSlotMoved(object? sender, HashSlotMovedEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_HashSlotMoved)}: {nameof(e.OldEndPoint)}-{e.OldEndPoint} To {nameof(e.NewEndPoint)}-{e.NewEndPoint}"));
        }

        /// <summary>
        /// 配置更改时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_ConfigurationChanged(object? sender, EndPointEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_ConfigurationChanged)}: {e.EndPoint}"));
        }

        /// <summary>
        /// 发生错误时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_ErrorMessage(object? sender, RedisErrorEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_ErrorMessage)}: {e.Message}"));
        }

        /// <summary>
        /// 物理连接失败时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Conn_ConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            OnInfoEventHandler(sender, new EventInfoResult(true, $"{nameof(Conn_ConnectionFailed)}: {e.Exception}"));
        }



        #endregion 注册事件

        #region String 操作
        /// <summary>
        /// 保存单个键值对字符串（如果 key 已存在，则覆盖值）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true表示成功）</returns>
        public bool StringSet(string key, string value)
        {
            if (GetStatus().Status)
            {

                return db.StringSet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value, new TimeSpan(0, 0, 0, 0, basics.Expiry));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取键对应值字符串
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（true表示成功）</returns>
        public string? StringGet(string key)
        {
            if (GetStatus().Status)
            {
                return db.StringGet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 保存多个多个键值对字符串
        /// </summary>
        /// <param name="keyValuePairs">键值对容器</param>
        /// <returns>返回结果（true表示成功）</returns>
        public bool StringSet(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            if (GetStatus().Status)
            {
                return db.StringSet(keyValuePairs.Select(x => new KeyValuePair<RedisKey, RedisValue>(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(x.Key) : x.Key, x.Value)).ToArray());
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 存储一个对象（该对象会被序列化保存）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值（会被序列化）</param>
        /// <returns>返回结果（true表示成功）</returns>
        public bool StringSet<T>(string key, T value)
        {
            if (GetStatus().Status)
            {
                return db.StringSet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value?.ToJson(), new TimeSpan(0, 0, 0, 0, basics.Expiry));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取一个对象（会进行反序列化）
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（true表示成功）</returns>
        public T? StringGet<T>(string key)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>(db.StringGet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key).ToString());
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// 保存一个键值对字符串（异步方式）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true表示成功）</returns>
        public async Task<bool> StringSetAsync(string key, string value)
        {
            if (GetStatus().Status)
            {
                return await db.StringSetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value, new TimeSpan(0, 0, 0, 0, basics.Expiry));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取单个值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="expiry">时间间隔</param>
        /// <returns>返回结果（true表示成功）</returns>
        public async Task<string?> StringGetAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.StringGetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 保存一组字符串值
        /// </summary>
        /// <param name="keyValuePairs">键值对容器</param>
        /// <returns>返回结果（true表示成功）</returns>
        public async Task<bool> StringSetAsync(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            if (GetStatus().Status)
            {
                return await db.StringSetAsync(keyValuePairs.Select(x => new KeyValuePair<RedisKey, RedisValue>(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(x.Key) : x.Key, x.Value)).ToArray());
            }
            return false;
        }

        /// <summary>
        /// 存储一个对象（该对象会被序列化保存）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true表示成功）</returns>
        public async Task<bool> StringSetAsync<T>(string key, T value)
        {
            if (GetStatus().Status)
            {
                return await db.StringSetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value?.ToJson(), new TimeSpan(0, 0, 0, 0, basics.Expiry));
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取一个对象（会进行反序列化）
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（true表示成功）</returns>
        public async Task<T?> StringGetAsync<T>(string key)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>((await db.StringGetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key)).ToString());
            }
            else
            {
                return default(T);
            }
        }

        #endregion String 操作

        #region List 操作

        /// <summary>
        /// 移除并返回存储在该键列表的第一个元素
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public string? ListLeftPop(string key)
        {
            if (GetStatus().Status)
            {
                return db.ListLeftPop(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的最后一个元素
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public string? ListRightPop(string key)
        {
            if (GetStatus().Status)
            {
                return db.ListRightPop(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 移除列表指定键上与该值相同的元素
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long ListRemove(string key, string value)
        {
            if (GetStatus().Status)
            {
                return db.ListRemove(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在列表尾部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long ListRightPush(string key, string value)
        {
            if (GetStatus().Status)
            {
                return db.ListRightPush(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在列表头部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long ListLeftPush(string key, string value)
        {
            if (GetStatus().Status)
            {
                return db.ListLeftPush(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 返回列表上该键的长度，如果不存在，返回 0
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long ListLength(string key)
        {
            if (GetStatus().Status)
            {
                return db.ListLength(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 返回在该列表上键所对应的元素
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="start">起始点</param>
        /// <param name="stop">停止点</param>
        /// <returns></returns>
        public IEnumerable<string?> ListRange(string key, long start = 0L, long stop = -1L)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(db.ListRange(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, start, stop));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的第一个元素对象
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回反序列化后对象</returns>
        public T? ListLeftPop<T>(string key)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>(db.ListLeftPop(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key).ToString());
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的最后一个元素对象
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回反序列化后的对象</returns>
        public T? ListRightPop<T>(string key)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>(db.ListRightPop(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key).ToString());
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// 在列表尾部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long ListRightPush<T>(string key, T value)
        {
            if (GetStatus().Status)
            {
                return db.ListRightPush(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value?.ToJson());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在列表头部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long ListLeftPush<T>(string key, T value)
        {
            if (GetStatus().Status)
            {
                return db.ListLeftPush(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value?.ToJson());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的第一个元素
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string?> ListLeftPopAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.ListLeftPopAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的最后一个元素
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public async Task<string?> ListRightPopAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.ListRightPopAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 移除列表指定键上与该值相同的元素
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> ListRemoveAsync(string key, string value)
        {
            if (GetStatus().Status)
            {
                return await db.ListRemoveAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在列表尾部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> ListRightPushAsync(string key, string value)
        {
            if (GetStatus().Status)
            {
                return await db.ListRightPushAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在列表头部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> ListLeftPushAsync(string key, string value)
        {
            if (GetStatus().Status)
            {
                return await db.ListLeftPushAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 返回列表上该键的长度，如果不存在，返回 0
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> ListLengthAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.ListLengthAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 返回在该列表上键所对应的元素
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public async Task<IEnumerable<string?>?> ListRangeAsync(string key, long start = 0L, long stop = -1L)
        {
            if (GetStatus().Status)
            {
                return db.ListRangeAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, start, stop).ConfigureAwait(false).GetAwaiter().GetResult().Select(x => x.ToString());
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的第一个元素对象
        /// </summary>
        /// <param name="key"></param>
        /// <returns>返回反序列化后的对象</returns>
        public async Task<T?> ListLeftPopAsync<T>(string key)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>((await db.ListLeftPopAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key)).ToString());
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// 移除并返回存储在该键列表的最后一个元素对象
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回反序列化后的对象</returns>
        public async Task<T?> ListRightPopAsync<T>(string key)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>((await db.ListRightPopAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key)).ToString());
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// 在列表尾部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> ListRightPushAsync<T>(string key, T value)
        {
            if (GetStatus().Status)
            {
                return await db.ListRightPushAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value?.ToJson());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在列表头部插入值。如果键不存在，先创建再插入值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> ListLeftPushAsync<T>(string key, T value)
        {
            if (GetStatus().Status)
            {
                return await db.ListLeftPushAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, value.ToJson());
            }
            else
            {
                return -1;
            }
        }

        #endregion List 操作

        #region Hash 操作

        /// <summary>
        /// 判断该字段是否存在hash中
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool HashExists(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return db.HashExists(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 从 hash 中移除指定字段
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool HashDelete(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return db.HashDelete(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 从 hash 中移除指定字段
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashFields">字段</param>
        /// <returns>返回结果（-1：表示失败）</returns>
        public long HashDelete(string key, IEnumerable<string> hashFields)
        {
            if (GetStatus().Status)
            {
                return db.HashDelete(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashFields.Select(x => (RedisValue)x).ToArray());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在 hash 设定值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool HashSet(string key, string hashField, string value)
        {
            if (GetStatus().Status)
            {
                return db.HashSet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField, value);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 在 hash 中设定值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashFields">字段容器</param>
        public void HashSet(string key, IEnumerable<KeyValuePair<string, string>> hashFields)
        {
            if (GetStatus().Status)
            {
                db.HashSet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashFields.Select(x => new HashEntry(x.Key, x.Value)).ToArray());
            }
        }

        /// <summary>
        /// 在 hash 中获取值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <returns>返回结果</returns>
        public string? HashGet(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return db.HashGet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 在hash获取值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashFields">字段</param>
        /// <returns>返回结果</returns>
        public IEnumerable<string?> HashGet(string key, IEnumerable<string> hashFields)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(db.HashGet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashFields.Select(x => (RedisValue)x).ToArray()));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 从 hash 返回所有的字段值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果</returns>
        public IEnumerable<string?> HashKeys(string key)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(db.HashKeys(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 根据键获取hash中的所有值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回hash结果</returns>
        public IEnumerable<string?> HashValues(string key)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(db.HashValues(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 在 hash 设定值（序列化）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true:表示成功）</returns>
        public bool HashSet<T>(string key, string hashField, T value)
        {
            if (GetStatus().Status)
            {
                return db.HashSet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField, value?.ToJson());
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 在 hash 中获取值（反序列化）
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashField"></param>
        /// <returns></returns>
        public T? HashGet<T>(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>(db.HashGet(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField).ToString());
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// 判断该字段是否存在hash中（异步方式）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <returns>返回结果（true：表示存在）</returns>
        public async Task<bool> HashExistsAsync(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return await db.HashExistsAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 从hash中移除指定字段
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public async Task<bool> HashDeleteAsync(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return await db.HashDeleteAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 从hash中移除指定字段
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashFields">字段</param>
        /// <returns>返回删除结果（-1 表示失败）</returns>
        public async Task<long> HashDeleteAsync(string key, IEnumerable<string> hashFields)
        {
            if (GetStatus().Status)
            {
                var fields = hashFields.Select(x => (RedisValue)x);

                return await db.HashDeleteAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, fields.ToArray());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 在 hash 设定值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true:表示成功）</returns>
        public async Task<bool> HashSetAsync(string key, string hashField, string value)
        {
            if (GetStatus().Status)
            {
                return await db.HashSetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField, value);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 在 hash 中设定值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashFields"></param>
        public async Task HashSetAsync(string key, IEnumerable<KeyValuePair<string, string>> hashFields)
        {
            if (GetStatus().Status)
            {
                await db.HashSetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashFields.Select(x => new HashEntry(x.Key, x.Value)).ToArray());
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// 在 hash 中获取值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <returns>返回结果</returns>
        public async Task<string?> HashGetAsync(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return await db.HashGetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 在 hash 中获取值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashFields">字段</param>
        /// <param name="value">值</param>
        /// <returns>返回结果</returns>
        public async Task<IEnumerable<string?>?> HashGetAsync(string key, IEnumerable<string> hashFields, string value)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(await db.HashGetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashFields.Select(x => (RedisValue)x).ToArray()));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 从 hash 返回所有的字段值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回对应的hash字段值</returns>
        public async Task<IEnumerable<string?>?> HashKeysAsync(string key)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(await db.HashKeysAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 返回 hash 中的所有值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回hash中的所有值</returns>
        public async Task<IEnumerable<string?>?> HashValuesAsync(string key)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(await db.HashValuesAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 在 hash 设定值（序列化）
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="hashField">字段</param>
        /// <param name="value">值</param>
        /// <returns>返回结果（true:表示成功）</returns>
        public async Task<bool> HashSetAsync<T>(string key, string hashField, T value)
        {
            if (GetStatus().Status)
            {
                return await db.HashSetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField, value?.ToJson());
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 在 hash 中获取值（反序列化）
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashField"></param>
        /// <returns></returns>
        public async Task<T?> HashGetAsync<T>(string key, string hashField)
        {
            if (GetStatus().Status)
            {
                return JsonSerializer.Deserialize<T>((await db.HashGetAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, hashField)).ToString());
            }
            else
            {
                return default(T);
            }
        }

        #endregion Hash 操作

        #region SortedSet 操作

        /// <summary>
        /// SortedSet 新增
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="member"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        public bool SortedSetAdd(string key, string member, double score)
        {
            if (GetStatus().Status)
            {
                return db.SortedSetAdd(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, member, score);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 在有序集合中返回指定范围的元素，默认情况下从低到高
        /// </summary>
        /// <param name="key"></param>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public IEnumerable<string> SortedSetRangeByRank(string key, long start = 0L, long stop = -1L, OrderType order = OrderType.Asc)
        {
            if (GetStatus().Status)
            {
                return db.SortedSetRangeByRank(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, start, stop, (Order)order).Select(x => x.ToString());
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 返回有序集合的元素个数
        /// </summary>
        /// <param name="key"></param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public long SortedSetLength(string key)
        {
            if (GetStatus().Status)
            {
                return db.SortedSetLength(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 从存储在键处的已排序集合中删除指定的成员。不存在的成员将被忽略
        /// </summary>
        /// <param name="key"></param>
        /// <param name="memebr"></param>
        /// <returns>返回结果（true:表示成功）</returns>
        public bool SortedSetRemove(string key, string memebr)
        {
            if (GetStatus().Status)
            {
                return db.SortedSetRemove(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, memebr);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// SortedSet 新增
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <param name="score"></param>
        /// <returns>返回结果（true:表示成功）</returns>
        public bool SortedSetAdd<T>(string key, T member, double score)
        {
            if (GetStatus().Status)
            {
                return db.SortedSetAdd(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, member?.ToJson(), score);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 增量的得分排序的集合中的成员存储键值键按增量
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <param name="value"></param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public double SortedSetIncrement(string key, string member, double value = 1)
        {
            if (GetStatus().Status)
            {
                return db.SortedSetIncrement(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, member, value);
            }
            else
            {
                return -1;
            }
        }
        /// <summary>
        /// SortedSet 新增
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <param name="score"></param>
        /// <returns>返回结果（true:表示成功）</returns>
        public async Task<bool> SortedSetAddAsync(string key, string member, double score)
        {
            if (GetStatus().Status)
            {
                return await db.SortedSetAddAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, member, score);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 在有序集合中返回指定范围的元素，默认情况下从低到高。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<IEnumerable<string?>?> SortedSetRangeByRankAsync(string key)
        {
            if (GetStatus().Status)
            {
                return ConvertStrings(await db.SortedSetRangeByRankAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key));
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 返回有序集合的元素个数
        /// </summary>
        /// <param name="key"></param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> SortedSetLengthAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.SortedSetLengthAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 返回有序集合的元素个数
        /// </summary>
        /// <param name="key"></param>
        /// <param name="memebr"></param>
        /// <returns>返回结果（true:表示成功）</returns>
        public async Task<bool> SortedSetRemoveAsync(string key, string memebr)
        {
            if (GetStatus().Status)
            {
                return await db.SortedSetRemoveAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, memebr);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// SortedSet 新增
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        public async Task<bool> SortedSetAddAsync<T>(string key, T member, double score)
        {
            if (GetStatus().Status)
            {
                return await db.SortedSetAddAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, member?.ToJson(), score);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 增量的得分排序的集合中的成员存储键值键按增量
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <param name="value"></param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<double> SortedSetIncrementAsync(string key, string member, double value = 1)
        {
            if (GetStatus().Status)
            {
                return await db.SortedSetIncrementAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, member, value);
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Redis 排序类型
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum OrderType
        {
            Asc,
            Desc
        }

        #endregion SortedSet 操作

        #region Key 操作

        /// <summary>
        /// 删除指定Key
        /// </summary>
        /// <param name="key">键(带*号支持模糊查询批量删除)</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool KeyDelete(string key)
        {
            key = !string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key;

            if (GetStatus().Status)
            {
                if (key.Contains("*"))
                {
                    RedisResult redisResult = FuzzyQuery(key).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!redisResult.IsNull)
                    {
                        return !KeyDelete((string[])redisResult).Equals(-1);
                    }
                    return false;
                }
                else
                {
                    return db.KeyDelete(key);
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 删除指定Key列表
        /// </summary>
        /// <param name="keys">键容器</param>
        /// <returns>移除指定键的数量(-1:表示失败)</returns>
        public long KeyDelete(IEnumerable<string> keys)
        {
            if (GetStatus().Status)
            {
                return db.KeyDelete(keys.Select(x => (RedisKey)x).ToArray());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 检查Key是否存在
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool KeyExists(string key)
        {
            if (GetStatus().Status)
            {
                return db.KeyExists(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 重命名Key
        /// </summary>
        /// <param name="key">原来的键</param>
        /// <param name="newKey">新的键名</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool KeyRename(string key, string newKey)
        {
            if (GetStatus().Status)
            {
                return db.KeyRename(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, newKey);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 设置 Key 的时间
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="expiry">时间间隔</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public bool KeyExpire(string key, TimeSpan? expiry)
        {
            if (GetStatus().Status)
            {
                return db.KeyExpire(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, expiry);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 移除指定 Key
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public async Task<bool> KeyDeleteAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.KeyDeleteAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 删除指定 Key 列表
        /// </summary>
        /// <param name="keys">键</param>
        /// <returns>返回结果（-1 表示失败）</returns>
        public async Task<long> KeyDeleteAsync(IEnumerable<string> keys)
        {
            if (GetStatus().Status)
            {
                return await db.KeyDeleteAsync(keys.Select(x => (RedisKey)x).ToArray());
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// 检查 Key 是否存在
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public async Task<bool> KeyExistsAsync(string key)
        {
            if (GetStatus().Status)
            {
                return await db.KeyExistsAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 重命名 Key
        /// </summary>
        /// <param name="key">旧的键名称</param>
        /// <param name="newKey">新的键名称</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public async Task<bool> KeyRenameAsync(string key, string newKey)
        {
            if (GetStatus().Status)
            {
                return await db.KeyRenameAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, newKey);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 设置 Key 的时间
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="expiry">间隔时间</param>
        /// <returns>返回结果（true：表示成功）</returns>
        public async Task<bool> KeyExpireAsync(string key, TimeSpan? expiry)
        {
            if (GetStatus().Status)
            {
                return await db.KeyExpireAsync(!string.IsNullOrWhiteSpace(basics.TAG) ? GTAG(key) : key, expiry);
            }
            else
            {
                return false;
            }
        }

        #endregion Key 操作

        #region 发布订阅 操作
        /// <summary>
        /// 订阅
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="handle"></param>
        public void Subscribe(RedisChannel channel, Action<RedisChannel, RedisValue> handle) => conn.GetSubscriber().Subscribe(channel, handle);

        /// <summary>
        /// 发布
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public long Publish(RedisChannel channel, RedisValue message) => conn.GetSubscriber().Publish(channel, message);

        /// <summary>
        /// 发布（使用序列化）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public long Publish<T>(RedisChannel channel, T message) => conn.GetSubscriber().Publish(channel, message?.ToJson());

        /// <summary>
        /// 订阅
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="handle"></param>
        public async Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handle) => await conn.GetSubscriber().SubscribeAsync(channel, handle);

        /// <summary>
        /// 发布
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<long> PublishAsync(RedisChannel channel, RedisValue message) => await conn.GetSubscriber().PublishAsync(channel, message);

        /// <summary>
        /// 发布（使用序列化）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<long> PublishAsync<T>(RedisChannel channel, T message) => await conn.GetSubscriber().PublishAsync(channel, message?.ToJson());

        #endregion 发布订阅 操作

        /// <summary>
        /// 获取TAG 前缀
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>返回前缀 + 键</returns>
        private string GTAG(string key)
        {
            return $"{basics.TAG}{key}";
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="list">数据列表</param>
        /// <returns>返回转为字符串后的数据列表</returns>
        private static IEnumerable<string?> ConvertStrings<T>(IEnumerable<T> list) where T : struct
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            return list.Select(x => x.ToString());
        }

        /// <summary>
        /// 模糊查询
        /// </summary>
        /// <param name="key">键</param>
        private async Task<RedisResult> FuzzyQuery(string key) => await db.ScriptEvaluateAsync(LuaScript.Prepare(
                " local res = redis.call('KEYS', @keypattern) " +
                " return res "), new { @keypattern = key });


    }
}

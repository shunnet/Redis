using System.ComponentModel;

namespace Snet.Redis
{
    /// <summary>
    /// 数据
    /// </summary>
    public class RedisData
    {
        /// <summary>
        /// 基础数据
        /// </summary>
        public class Basics
        {
            /// <summary>
            /// 标识 前缀
            /// </summary>
            [Category("基础数据")]
            [Description("标识，前缀")]
            public string TAG { get; set; } = "S:";
            /// <summary>
            /// 连接字符串
            /// </summary>
            [Description("连接字符串")]
            public string ConnectStr { get; set; }
            /// <summary>
            /// 数据库ID
            /// </summary>
            [Description("数据库ID")]
            public int DataBaseID { get; set; } = 0;
            /// <summary>
            /// 有效时间 ms
            /// </summary>
            [Description("有效时间(ms)")]
            public int Expiry { get; set; } = 86400000;
        }
    }
}

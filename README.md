# <img src="https://api.shunnet.top/pic/nuget.png" height="28"> Redis  

> 🚀 **高性能 · 内存数据库 · 缓存 & 消息队列**  


## 📖 简介

在现代系统中，`快速响应 / 缓存 / 会话存储 / 队列` 等应用场景越来越常见。  
**shunnet/Redis** 致力于提供一个 **简洁 · 可靠 · 高性能** 的内存数据库解决方案，作为缓存或服务组件的数据层支撑。  

✨ 特点：
- ⚡ **极致性能**：数据操作均在内存中完成，避免磁盘 IO  
- 🛠 **简洁接口**：支持字符串、哈希、列表、集合、有序集合等操作  
- 🧩 **模块化 & 可扩展**：可灵活插入业务逻辑  
- 🖥 **跨平台部署**：支持 Windows / Linux / Docker  
- 🌱 **轻量设计**：无冗余依赖，快速集成  


## 🎯 适用场景

- 🔹 缓存层加速读写  
- 🔹 会话 / 状态管理  
- 🔹 简单队列 & 发布-订阅  
- 🔹 边缘 / 嵌入式服务组件  
- 🔹 微服务快速中间存储  


## 🚀 快速开始

### 🔧 安装 / 构建

```bash
git clone https://github.com/shunnet/Redis.git

//打开运行 Snet.Redis.Samples 即可
```

### 💡 使用示例

```csharp
using Snet.Log;
using Snet.Redis;
using Snet.Utility;

RedisOperate redisOperate = RedisOperate.Instance(new RedisData.Basics
{
    ConnectStr = "127.0.0.1:6379",
    DataBaseID = 0,
    Expiry = 86400000,
    TAG = "Samples:",
});

Console.WriteLine(redisOperate.Title());

LogHelper.Info(redisOperate.On().ToJson(true));
LogHelper.Info(redisOperate.KeyDelete("*").ToString());

for (int i = 0; i < 10000; i++)
{
    LogHelper.Info(i.ToString());
    string xmlstr = new data().ToXml();
    LogHelper.Info(redisOperate.StringSet($"A{i}", xmlstr).ToString());
    data? xml = redisOperate.StringGet($"A{i}")?.ToXmlEntity<data>();
    Console.WriteLine(xml.ToJson(true));
}

LogHelper.Info(redisOperate.Off().ToJson(true));

public class data
{
    public double a { get; set; } = new Random().NextDouble();
    public double b { get; set; } = new Random().NextDouble();
    public double c { get; set; } = new Random().NextDouble();
    public double d { get; set; } = new Random().NextDouble();
    public double e { get; set; } = new Random().NextDouble();
    public double f { get; set; } = new Random().NextDouble();
    public double g { get; set; } = new Random().NextDouble();
}
```


## 🔑 核心功能

| 模块 / 功能        | 描述 |
|---------------------|------|
| 🔹 字符串 (String)  | 基础的 key-value 操作 |
| 🔹 哈希 (Hash)      | 存储对象字段、支持批量操作 |
| 🔹 列表 (List)      | 支持 push/pop、范围查询 |
| 🔹 集合 (Set)       | 无序集合，支持交并差运算 |
| 🔹 有序集合 (ZSet)  | 按分数排序的集合检索 |
| 🔹 发布 / 订阅 (Pub/Sub) | 支持消息通信机制 |


## 🙏 致谢  

- 🌐 [Shunnet.top](https://shunnet.top)  
- 🔥 [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)  


## 📜 许可证  

![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)  

本项目基于 **MIT** 协议开源。  
详情请阅读 [LICENSE](LICENSE)。  

⚠️ 注意：本软件按 “原样” 提供，作者不对使用后果承担责任。  

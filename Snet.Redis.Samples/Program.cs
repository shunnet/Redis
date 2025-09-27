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
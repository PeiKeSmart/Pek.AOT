using Pek.Http;
using Pek.Log;
using Pek.Serialization;

using System.Text.Json.Serialization;

XTrace.UseConsole();
JsonHelper.Register(HttpServerSampleJsonContext.Default.SampleItem);

var server = new HttpServer
{
    Port = 0,
};
server.Log = XTrace.Log;
server.SessionLog = XTrace.Log;
server.SocketLog = XTrace.Log;

server.Map("/ping", () => "pong");
server.MapAction<UserController, Int32>(
    "Find",
    ControllerActionRegistry.FromParameter<Int32>("id"),
    (controller, id) => controller.Find(id),
    "/User");
server.MapControllerActions<ProductController>("/Product");

server.Start();

Ensure(server.Port > 0, $"HttpServer 未获取到有效端口：{server.Port}");
Console.WriteLine($"HttpServer listening on http://127.0.0.1:{server.Port}");

using var client = new HttpClient
{
    BaseAddress = new Uri($"http://127.0.0.1:{server.Port}"),
    Timeout = TimeSpan.FromSeconds(5),
};

try
{
    var ping = await client.GetStringAsync("/ping");
    Ensure(ping == "pong", $"/ping 响应异常：{ping}");

    var user = await client.GetStringAsync("/User/Find?id=7");
    Ensure(user.Contains("\"Id\":7", StringComparison.Ordinal), $"/User/Find 响应异常：{user}");

    var product = await client.GetStringAsync("/Product/Detail?id=9");
    Ensure(product.Contains("\"Id\":9", StringComparison.Ordinal), $"/Product/Detail 响应异常：{product}");

    Console.WriteLine("HttpServer sample passed.");
}
finally
{
    server.Stop("SampleComplete");
}

static void Ensure(Boolean condition, String message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class UserController
{
    public Object? Find(Int32 id) => new SampleItem { Id = id, Name = "User-" + id };
}

sealed class ProductController : IHttpControllerRegistration<ProductController>
{
    public static void MapActions(HttpServer server, String? path = null)
    {
        server.MapAction<ProductController, Int32>(
            "Detail",
            ControllerActionRegistry.FromParameter<Int32>("id"),
            (controller, id) => controller.Detail(id),
            path);
    }

    public Object? Detail(Int32 id) => new SampleItem { Id = id, Name = "Product-" + id };
}

sealed class SampleItem
{
    public Int32 Id { get; set; }

    public String? Name { get; set; }
}

[JsonSerializable(typeof(SampleItem))]
partial class HttpServerSampleJsonContext : JsonSerializerContext
{
}
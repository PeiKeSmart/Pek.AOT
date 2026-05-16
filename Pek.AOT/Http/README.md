# HTTP AOT 用法

当前目录中的 HTTP 服务端实现已经支持 AOT/Trim 安全的以下能力：

- `HttpServer` 路由匹配与 `HttpSession` 请求处理
- `StaticFilesHandler` 静态文件映射
- `DelegateHandler` 固定签名委托分发
- `ControllerHandler` 显式 action 注册
- `IHttpControllerRegistration<TController>` 静态控制器注册约定

## 基础路由

```csharp
var server = new HttpServer();

server.Map("/ping", () => "pong");
server.Map("/time", ctx => DateTime.Now.ToString("O"));
server.MapStaticFiles("/wwwroot", "wwwroot");
```

## 显式控制器 Action 注册

```csharp
JsonHelper.Register(HttpSampleJsonContext.Default.SampleItem);

public class UserController
{
    public SampleItem? Find(Int32 id) => new SampleItem { Id = id, Name = "User-" + id };
}

var server = new HttpServer();
server.MapController<UserController>();
server.MapAction<UserController, Int32>(
    "Find",
    ControllerActionRegistry.FromParameter<Int32>("id"),
    (controller, id) => controller.Find(id));
```

```csharp
public class SampleItem
{
    public Int32 Id { get; set; }

    public String? Name { get; set; }
}

[JsonSerializable(typeof(SampleItem))]
public partial class HttpSampleJsonContext : JsonSerializerContext
{
}
```

请求路径格式保持为 `/{ControllerName}/{ActionName}`，例如 `/User/Find?id=123`。

## 静态控制器注册约定

如果希望控制器自行声明其 actions，可实现 `IHttpControllerRegistration<TController>`：

```csharp
public class ProductController : IHttpControllerRegistration<ProductController>
{
    public static void MapActions(HttpServer server, String? path = null)
    {
        server.MapAction<ProductController, Int32>(
            "Detail",
            ControllerActionRegistry.FromParameter<Int32>("id"),
            (controller, id) => controller.GetDetail(id),
            path);
    }

    public SampleItem? GetDetail(Int32 id) => new SampleItem { Id = id, Name = "Product-" + id };
}

var server = new HttpServer();
server.MapControllerActions<ProductController>();
```

该模式是当前仓库对“自动动作发现”的 AOT 安全替代：

- 不扫描运行时方法
- 不使用 `MethodInfo.Invoke`
- 不依赖动态代理或表达式编译
- 所有 action 绑定都在编译期显式声明

## 参数绑定器

当前可用绑定器：

- `ControllerActionRegistry.FromParameter<T>(name)`：从 `context.Parameters` 读取
- `ControllerActionRegistry.FromBody<T>()`：从请求体或参数字典绑定
- `ControllerActionRegistry.FromService<T>()`：从 `IServiceProvider` 解析
- `ControllerActionRegistry.FromContext<T>()`：绑定 `IHttpContext`、`HttpRequest`、`HttpResponse`、`INetSession`、`ISocketRemote`、`WebSocket`

## 当前边界

匿名对象不会自动获得 AOT 安全 JSON 序列化支持；如果 action 需要返回对象，请使用显式 DTO，并先注册对应 `JsonTypeInfo`。

当前仓库还提供了 `HttpControllerAttribute`、`HttpActionAttribute`、`HttpFromParameterAttribute`、`HttpFromBodyAttribute`、`HttpFromServiceAttribute`、`HttpFromContextAttribute` 作为未来源生成注册的标记契约。

这些特性当前不会被运行时自动扫描；它们的作用是为后续源生成器接入保留稳定元数据表面，而不是恢复运行时反射发现。

当前实现有意不包含以下能力：

- 基于运行时反射的 action 自动发现
- 基于参数名和 `MethodInfo` 的通用方法调用
- 控制器方法的隐式扫描与动态绑定

如果后续需要进一步减少手工注册量，优先考虑源生成注册，而不是恢复运行时反射主链。
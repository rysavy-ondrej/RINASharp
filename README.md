RINASharp
=========
This project represents an experimental implementation of RINA for .NET. This
implementation is inspired by the development in the EU project PRISTINE (http://ict-pristine.eu/).

Currently, there are a lot of empty classes and only a few working code.
It is possible to run a simple demo application representing TimeService.
Because only implemented DIF if ShimDIF using WCF the recursive principle
of RINA layers cannot be demonstrated yet. However, the demonstration provides
hint on how RINA communication may look likes.

## TimeService Demo
Server side consists of implementation of a server that answers to request of the clients.

The main method just creates IpcHost instance that manages all IpcProcess instances
in the application. As the only DIF, the ShimDIF is considered and the process
shimIpcProcess ius created in this DIF. Application is registered in the
Shim DIF process. This causes that the application can receives request from
clients.

```CSharp
static void Main(string[] args)
     {
         using (var host = new IpcHost())
         {
             var shimIpcProcess = WcfServiceIpcProcess.Create("server");

             shimIpcProcess.RegisterApplication(new ApplicationNamingInfo("TimeServer", "1", "TimeServiceProtocol", "V1"), applicationRequestHandler);

             var evh = new EventWaitHandle(false, EventResetMode.AutoReset);
             Console.CancelKeyPress += new ConsoleCancelEventHandler((x, a) => evh.Set());
             evh.WaitOne();
         }                      
     }
```
Then two handlers needs to be implemented. First handler processes request
for connection from clients. In the implementation shown bellow, we accept any client:
```CSharp
private static ConnectionRequestResult applicationRequestHandler(IRinaIpc context, FlowInformation flowInformation, out AcceptFlowHandler acceptFlowHandler)
{
    Console.WriteLine($"FlowInfo:{flowInformation.SourceApplication}@{flowInformation.SourceAddress} <-> {flowInformation.DestinationApplication}@{flowInformation.DestinationAddress}");
    acceptFlowHandler = ServerProgram.acceptFlowHandler;
    return ConnectionRequestResult.Accept;
}
```
Other handler provides Task that is used to process connected client requests.
The implementation creates StreamReader and StreamWriter classes to ease
communication with the client. The protocol is simple. The client sends
request command and awaits the response, which is either ERROR or OK.
```CSharp
private static Task acceptFlowHandler(IRinaIpc context, FlowInformation flowInformation, Port port)
{
   return Task.Run(() =>
   {
       port.Blocking = true;
       using (var stream = new IpcChannelStream(port))
       using (var tr = new StreamReader(stream))
       using (var tw = new StreamWriter(stream))
       {
           var line = tr.ReadLine();
           switch ((line ?? String.Empty).Trim())
           {
               case "DateTime.Now": tw.WriteLine($"OK: {DateTime.Now}"); break;
               default: tw.WriteLine("ERROR: Invalid request!"); break;
           }
       }
   });
}
```


# Naming and addressing

## RINA URI
Within a DIF it is possible to use URI style addressing:
```
rina://DIF-NAME/IPC-NAME/RIB-PATH
```
Some well-known RIB-paths are:

| Path | Meaning  |
|-----------------------------------------------------------------------|
| flows/FLOW-ID/data | provides flow data access            |
| flows/FLOW-ID/control | provides control information for flow |
| apps/APPLICATION-NAME | access to application |
| apps/APPLICATION-NAME/INSTANCE | identifies application process |

### FLOWS subpath
Flows subpath can be used to control flow management. To create a new flow
the client sends a request using the following snippet:
Request a new flow:
```CSharp
var ci = new ConnectionInformation()
       {               
           SourceAddress = "rina://ethershim/00155d08f6e3e",
           SourceApplication = "SensorReader",
           DestinationAddress = "rina://ethershim/00155d0c6404",
           DestinationApplication = "TempSensor"
       };

var openReq = new IpcOpenRequest()
      {
          ObjectClass = "Flow",
          ObjectName = "/flows/",
          ObjectValue = ci
      };

var port = ipc.AllocateFlow("rina://ethershim/00155d0c6404");

ipc.Send(port, openReq)
```

### APPS subpath
Identifies a web server represented by iis application with pid 4759
that runs on Dublin host.
```
rina://wcfshim/Dublin/apps/iis.exe/4759
```
Note that application NAME can
be an alias. So, it is possible to define names for well-known services and maps
them to specific providers.
```
rina://wcfshim/Dublin/apps/web-server
```
This is an alias that maps to iis application.

## Flow Lifetime

### Flow Connecting
Usually, clients initializes connection to server application:
```CSharp
var port = ipc.Connect(flowInformation);
if (port != null)
{
...
}
```
When client calls `Connect` the following procedure is executed:
* Client calls `Connect`, which generates `ConnectRequest` message sent to server.
* Server receives `ConnectRequest`, validates this request and finds an application to serve to this request
* If application can server the request the server sends `ConnectResponse` with result `ConnectResult.Accept`; otherwise result is `ConnectResult.Reject`.
* Client receives `ConnectResponse` and depending on the `ConnectResult` value,  it initializes local connection endpoint and creates port or return null port.


### Flow Disconnecting
When client wants to close the connection gracefully it can call Disconnect function:
```CSharp
ipc.Disconnect(port, timeout);
```
or asynchronous version:
```CSharp
var ct = new CancellationToken();
await ipc.DisconnectAsync(port, ct);
```
When client initiates disconnection, the order of actions is as follows:
* Client calls `Disconnect` and sends `DisconnectRequest` message of type `Gracefull`
* Server receives `DisconnectRequest` message, which indicates that no more data will be received
* If server has buffered data to send it processes this data
* Client keeps receiving data from the server
* Server has finished sending pending data and sends `DisconnectResponse`
* Client closes the connection upon receiving `DisconnectResponse` message.

Other option is to abort the connection causing that pending data will be discarded:
```CSharp
ipc.Abort(port);
```
* Client calls `Abort` that causes `DisconnectRequest` message of type Abort will be sent
* When Server receives `DisconnectRequest` message of type `Abort`, it should discard all messages
and may optionally send `DisconnectRespose` message
* Client will not process any other messages except `DisconnectResponse` message.






# Winsock 2
The goal is to implement RINA in as a new service provider in Winsock 2.
Some useful references related to Winsock 2 programming:
* https://msdn.microsoft.com/en-us/library/windows/desktop/ms740673(v=vs.85).aspx

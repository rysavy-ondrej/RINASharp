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

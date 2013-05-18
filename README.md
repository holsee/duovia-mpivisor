duovia-mpivisor
==========

DuoVia.MpiVisor
----------
### Distributed Parallel Computing for .NET Mortals.

> Writing distributed parallel computing applications is easy with DuoVia.MpiVisor.
>
> Get the "agent" client library via [NuGet][1]. To run across multiple cluster nodes, get the source and build the server service.
>
> Write a simple console app and debug it in Visual Studio as if it were runnning across multiple MpiVisor cluster server nodes. Add break points to your code for the master and the spawned agent code. 
>
> Once you have your code running locally, flip the switch and you'll be running it across as many servers as you have with as many instances as you want.
>
> This code was inspired by Frank Thomsen's [MPAPI][2] on Codeplex and [MPI.NET][3] from Indiana University. Unlike MPI.NET, the DuoVia.MpiVisor does not rely on Microsoft HPC. And unlike MPAPI, DuoVia.MpiVisor does not create a TCP/IP endpoint for each worker or unique application node. Rather a single cluster server service run on each node in the distributed processing cluster. These cluster nodes orchestrate distribution and communication between agents.
>
> More to come... 
> 
> The author hopes you will use and help improve this library. Join the conversation on [Hip Chat][4].

[1]: http://nuget.org/packages/DuoVia.MpiVisor/    "NuGet"
[2]: http://mpapi.codeplex.com/          "MPAPI"
[3]: http://osl.iu.edu/research/mpi.net/   "MPI.NET"
[4]: https://www.hipchat.com/gHWO84CXp     "Hip Chat"

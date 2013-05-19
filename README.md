DuoVia.MpiVisor
=======

### Distributed Parallel Computing for .NET Mortals.

Writing distributed parallel computing applications is easy with DuoVia.MpiVisor.
Get the "agent" client library via [NuGet][1]. To run across multiple cluster nodes, get the source and build the server service.
Write a simple console app and debug it in Visual Studio as if it were runnning across multiple MpiVisor cluster server nodes. Add break points to your code for the master and the spawned agent code. 
Once you have your code running locally, flip the switch and you'll be running it across as many servers as you have with as many instances as you want.

### CONTRIBUTE

We hope you will use and help improve this library.  We take pull requests!
Join the conversation in our open [Hip Chat][4].

### LICESNE (APACHE V2)
  
Copyright (C) 2013 Duovia Inc. & Steven Holdsworth
Authors: Duovia Inc. & Steven Holdsworth  
Source: https://github.com/duovia/duovia-mpivisor
  
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
 
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

[1]: http://nuget.org/packages/DuoVia.MpiVisor/    "NuGet"
[2]: http://mpapi.codeplex.com/          "MPAPI"
[3]: http://osl.iu.edu/research/mpi.net/   "MPI.NET"
[4]: https://www.hipchat.com/gHWO84CXp     "Hip Chat"
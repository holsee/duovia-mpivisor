using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;

namespace DuoVia.MpiVisor.Server
{
    [RunInstaller(true)]
    public partial class MpiVisorServiceInstaller : System.Configuration.Install.Installer
    {
        public MpiVisorServiceInstaller()
        {
            InitializeComponent();
        }
    }
}

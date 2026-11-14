using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;

namespace batocera_bluetooth {
    internal static class LowEnergy {
        const string Address = "System.Devices.Aep.DeviceAddress";
        const string Appearance = "System.Devices.Aep.Bluetooth.Le.Appearance";
        static readonly string[] Fields = { Address, Appearance };
        static string Mac(DeviceInformation device) {
            object value;
            if(device.Properties.TryGetValue(Address,out value) && value!=null) return value.ToString().Replace('-',':').ToUpperInvariant();
            var match=Regex.Match(device.Id,@"([0-9A-F]{2}:){5}[0-9A-F]{2}$",RegexOptions.IgnoreCase);
            return match.Value.ToUpperInvariant();
        }
        static string Cache(string mac){return System.IO.Path.Combine(System.IO.Path.GetTempPath(),"RetroBat-LE-"+mac.Replace(":","")+".id");}
        static bool IsPad(DeviceInformation device) {
            object value;
            int appearance;
            if(device.Properties.TryGetValue(Appearance,out value) && int.TryParse(Convert.ToString(value), out appearance) && (appearance==963 || appearance==964)) return true;
            return Regex.IsMatch(device.Name ?? "", @"xbox|gamepad|wireless controller|dualsense|dualshock|8bitdo|pro controller",RegexOptions.IgnoreCase);
        }
        static T Wait<T>(IAsyncOperation<T> op) {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(op.Status==AsyncStatus.Started) {
                if(timer.ElapsedMilliseconds>45000){op.Cancel();throw new TimeoutException();}
                Thread.Sleep(50);
            }
            return op.GetResults();
        }
        static string Line(DeviceInformation d,string state) {
            return "<device id=\""+Mac(d)+"\" name=\""+SecurityElement.Escape(d.Name+(state=="added" && d.Pairing.IsPaired?" (bereits gekoppelt)":""))+"\" type=\""+(IsPad(d)?"joystick":"unknown")+"\""+(state==null?"":" status=\""+state+"\"")+"/>";
        }
        internal static DeviceInformation[] Paired() {
            var selector=BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            return Wait(DeviceInformation.FindAllAsync(selector,Fields,DeviceInformationKind.AssociationEndpoint)).Where(IsPad).ToArray();
        }
        internal static void List() {foreach(var d in Paired()) Console.WriteLine(Line(d,null));}
        internal static bool Handle(string command,string address) {
            ulong number;
            if(!ulong.TryParse(address.Replace(":",""),System.Globalization.NumberStyles.HexNumber,null,out number))return false;
            var selector=BluetoothLEDevice.GetDeviceSelectorFromBluetoothAddress(number);
            var d=Paired().FirstOrDefault(row=>Mac(row).Equals(address,StringComparison.OrdinalIgnoreCase));
            string cache=Cache(address);
            if(d==null && System.IO.File.Exists(cache)){
                try{d=Wait(DeviceInformation.CreateFromIdAsync(System.IO.File.ReadAllText(cache),Fields,DeviceInformationKind.AssociationEndpoint));}catch{}
                if(d!=null && !Mac(d).Equals(address,StringComparison.OrdinalIgnoreCase))d=null;
            }
            if(d==null && !System.IO.File.Exists(cache))return false;
            if(d==null)d=Wait(DeviceInformation.FindAllAsync(selector,Fields,DeviceInformationKind.AssociationEndpoint)).FirstOrDefault();
            if(d==null)return false;
            if(command=="remove"){if(d.Pairing.IsPaired && Wait(d.Pairing.UnpairAsync()).Status!=DeviceUnpairingResultStatus.Unpaired) Environment.ExitCode=1;}
            else if(command=="trust" || command=="connect"){
                if(!d.Pairing.IsPaired && !Pair(d)) Environment.ExitCode=1;
            }
            return true;
        }
        static bool Pair(DeviceInformation d){
            var custom=d.Pairing.Custom;
            Windows.Foundation.TypedEventHandler<DeviceInformationCustomPairing,DevicePairingRequestedEventArgs> confirm=(s,e)=>{if(e.PairingKind==DevicePairingKinds.ConfirmOnly)e.Accept();};
            custom.PairingRequested+=confirm;
            try{
                var status=Wait(custom.PairAsync(DevicePairingKinds.ConfirmOnly)).Status;
                return status==DevicePairingResultStatus.Paired || status==DevicePairingResultStatus.AlreadyPaired;
            }finally{custom.PairingRequested-=confirm;}
        }
        internal static DeviceWatcher Watch(bool automatic){
            var w=DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),Fields,DeviceInformationKind.AssociationEndpoint);
            var seen=new Dictionary<string,DeviceInformation>();
            var shown=new HashSet<string>();
            int pairing=0;
            Action<DeviceInformation> publish=d=>{
                if(Mac(d)=="" || d.Pairing.IsPaired || String.IsNullOrWhiteSpace(d.Name))return;
                if(automatic ? !IsPad(d) : (!IsPad(d) && !d.Pairing.CanPair))return;
                if(!shown.Add(d.Id))return;
                System.IO.File.WriteAllText(Cache(Mac(d)),d.Id);
                if(!automatic){Console.WriteLine(Line(d,"added"));return;}
                if(Interlocked.CompareExchange(ref pairing,1,0)==0)ThreadPool.QueueUserWorkItem(o=>{
                    try{if(Pair(d)){Console.WriteLine("Found \""+d.Name+", "+Mac(d)+"\"");Environment.Exit(0);}}catch(Exception e){Console.Error.WriteLine(e.Message);}finally{Interlocked.Exchange(ref pairing,0);}
                });
            };
            w.Added+=(s,d)=>{lock(seen){seen[d.Id]=d;publish(d);}};
            w.Updated+=(s,u)=>{lock(seen){DeviceInformation d;if(seen.TryGetValue(u.Id,out d)){d.Update(u);publish(d);}}};
            w.Removed+=(s,u)=>{lock(seen){DeviceInformation d;if(seen.TryGetValue(u.Id,out d)&&shown.Remove(u.Id)&&!automatic)Console.WriteLine(Line(d,"removed"));seen.Remove(u.Id);}};
            w.Start();
            if(!automatic) foreach(var d in Paired())Console.WriteLine(Line(d,"added"));
            return w;
        }
    }
}

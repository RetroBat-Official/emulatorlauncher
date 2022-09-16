using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using emulatorLauncher.Tools;
using System.IO;

namespace emulatorLauncher.PadToKeyboard
{
    [DataContract]
    public class EvMapyKeysFile : IEnumerable<List<EvMapyAction>>
    {
        public static EvMapyKeysFile TryLoad(string fileName)
        {
            if (!File.Exists(fileName))
                return null;

            try
            {
                EvMapyKeysFile ret = JsonSerializer.DeserializeFile<EvMapyKeysFile>(fileName);
                if (ret != null)
                {
                    SimpleLogger.Instance.Info("[Pad2Key] loaded " + fileName);
                    return ret;
                }
            }
            catch
            {
                
            }

            return null;
        }


        [DataMember]
        public List<EvMapyAction> actions_player1 { get; set; }
        [DataMember]
        public List<EvMapyAction> actions_player2 { get; set; }
        [DataMember]
        public List<EvMapyAction> actions_player3 { get; set; }
        [DataMember]
        public List<EvMapyAction> actions_player4 { get; set; }

        public int Count
        {
            get
            {
                for (int i = 0; i < 4; i++)
                    if (this[i] == null)
                        return i;

                return 0;
            }
        }

        public List<EvMapyAction> this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return actions_player1;
                    case 1:
                        return actions_player2;
                    case 2:
                        return actions_player3;
                    case 3:
                        return actions_player4;
                }

                return null;
            }
        }

        public IEnumerator<List<EvMapyAction>> GetEnumerator()
        {
            List<List<EvMapyAction>> ret = new List<List<EvMapyAction>>();

            for (int i = 0; i < 4; i++)
                ret.Add(this[i]);

            return ret.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            List<List<EvMapyAction>> ret = new List<List<EvMapyAction>>();

            for (int i = 0; i < 4; i++)
                ret.Add(this[i]);

            return ret.GetEnumerator();
        }
    }

    [DataContract]
    public class EvMapyAction
    {

        public string[] Triggers
        {
            get
            {
                if (trigger is object[])
                    return ((object[])trigger).OfType<string>().ToArray();

                if (trigger is string)
                    return new string[] { (string) trigger };

                return new string[] {};
            }
        }

        public string[] Targets
        {
            get
            {
                if (target is object[])
                    return ((object[])target).OfType<string>().ToArray();

                if (target is string)
                    return new string[] { (string)target };

                return new string[] { };
            }
        }

        [DataMember]
        object trigger { get; set; }

        [DataMember]
        public string type { get; set; }

        [DataMember]
        public string mode { get; set; }

        [DataMember]
        object target { get; set; }


        public override string ToString()
        {
            return string.Join("+", Triggers) + " => " + string.Join("+", Targets);
        }
    }
}

using System;
using System.Runtime.Serialization;

namespace WindBot
{
    [DataContract]
    public class CreateGameInfo
    {
        private uint _banlistHash = 0;
        private byte _allowed = 3;
        private bool _dontCheckDeck = false;
        private bool _dontShuffleDeck = false;
        private uint _startingLP = 8000;
        private byte _startingDrawCount = 5;
        private byte _drawCountPerTurn = 1;
        private ushort _timeLimitInSeconds = 180;
        private ulong _duelFlags = 190464;
        private int _t0Count = 1;
        private int _t1Count = 1;
        private int _bestOf = 1;
        private int _forb = 0;
        private ushort _extraRules = 0;
        private string _notes = "";

        [DataMember]
        public uint banlistHash
        {
            get { return _banlistHash; }
            set { _banlistHash = value; }
        }

        [DataMember]
        public byte allowed
        {
            get { return _allowed; }
            set { _allowed = value; }
        }

        [DataMember]
        public bool dontCheckDeck
        {
            get { return _dontCheckDeck; }
            set { _dontCheckDeck = value; }
        }

        [DataMember]
        public bool dontShuffleDeck
        {
            get { return _dontShuffleDeck; }
            set { _dontShuffleDeck = value; }
        }

        [DataMember]
        public uint startingLP
        {
            get { return _startingLP; }
            set { _startingLP = value; }
        }

        [DataMember]
        public byte startingDrawCount
        {
            get { return _startingDrawCount; }
            set { _startingDrawCount = value; }
        }

        [DataMember]
        public byte drawCountPerTurn
        {
            get { return _drawCountPerTurn; }
            set { _drawCountPerTurn = value; }
        }

        [DataMember]
        public ushort timeLimitInSeconds
        {
            get { return _timeLimitInSeconds; }
            set { _timeLimitInSeconds = value; }
        }

        [DataMember]
        public ulong duelFlags
        {
            get { return _duelFlags; }
            set { _duelFlags = value; }
        }

        [DataMember]
        public int t0Count
        {
            get { return _t0Count; }
            set { _t0Count = value; }
        }

        [DataMember]
        public int t1Count
        {
            get { return _t1Count; }
            set { _t1Count = value; }
        }

        [DataMember]
        public int bestOf
        {
            get { return _bestOf; }
            set { _bestOf = value; }
        }

        [DataMember]
        public int forb
        {
            get { return _forb; }
            set { _forb = value; }
        }

        [DataMember]
        public ushort extraRules
        {
            get { return _extraRules; }
            set { _extraRules = value; }
        }

        [DataMember]
        public string notes
        {
            get { return _notes; }
            set { _notes = value; }
        }
    }

    [DataContract]
    public class WindBotInfo
    {
        private string _Name = "WindBot";
        private string _Deck = null;
        private string _DeckFile = null;
        private string _Dialog = "default";
        private string _Host = "127.0.0.1";
        private int _Port = 7911;
        private string _HostInfo = "";
        private int _Version = 40|1<<8|10<<16;
        private int _Hand = 0;
        private bool _Debug = false;
        private bool _Chat = true;
        private int _RoomId = 0;
        private bool _IsFirst = true;
        private CreateGameInfo _CreateGame = null;

        [DataMember]
        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        [DataMember]
        public string Deck
        {
            get { return _Deck; }
            set { _Deck = value; }
        }

        [DataMember]
        public string DeckFile
        {
            get { return _DeckFile; }
            set { _DeckFile = value; }
        }

        [DataMember]
        public string Dialog
        {
            get { return _Dialog; }
            set { _Dialog = value; }
        }

        [DataMember]
        public string Host
        {
            get { return _Host; }
            set { _Host = value; }
        }

        [DataMember]
        public int Port
        {
            get { return _Port; }
            set { _Port = value; }
        }

        [DataMember]
        public string HostInfo
        {
            get { return _HostInfo; }
            set { _HostInfo = value; }
        }

        [DataMember]
        public int Version
        {
            get { return _Version; }
            set { _Version = value; }
        }

        [DataMember]
        public int Hand
        {
            get { return _Hand; }
            set { _Hand = value; }
        }

        [DataMember]
        public bool Debug
        {
            get { return _Debug; }
            set { _Debug = value; }
        }

        [DataMember]
        public bool Chat
        {
            get { return _Chat; }
            set { _Chat = value; }
        }

        [DataMember]
        public int RoomId
        {
            get { return _RoomId; }
            set { _RoomId = value; }
        }

        [DataMember]
        public bool IsFirst
        {
            get { return _IsFirst; }
            set { _IsFirst = value; }
        }

        [DataMember]
        public CreateGameInfo CreateGame
        {
            get { return _CreateGame; }
            set { _CreateGame = value; }
        }

        public WindBotInfo()
        {
        }
    }
}

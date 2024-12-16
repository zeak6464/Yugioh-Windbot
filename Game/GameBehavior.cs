using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using WindBot.Game.AI;
using YGOSharp.Network;
using YGOSharp.Network.Enums;
using YGOSharp.Network.Utils;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game.AI.Decks;

namespace WindBot.Game
{
    public class GameBehavior
    {
        public GameClient Game { get; private set; }
        public YGOClient Connection { get; private set; }
        public Deck Deck { get; private set; }

        private GameAI _ai;

        private IDictionary<StocMessage, Action<BinaryReader>> _packets;
        private IDictionary<GameMessage, Action<BinaryReader>> _messages;

        private Room _room;
        private Duel _duel;
        private int _hand;
        private bool _debug;
        private long _select_hint;
        private GameMessage _lastMessage;
        public class LocationInfo
        {
            public int controler;
            public int location;
            public int sequence;
            public int position;
            public LocationInfo()
            {
                controler = 0;
                location = 0;
                sequence = 0;
                position = 0;
            }
            public LocationInfo(BinaryReader packet, bool isfirst = false)
            {
                Read(packet, isfirst);
            }
            public void Read(BinaryReader packet, bool isfirst = false)
            {
                controler = packet.ReadByte();
                if(!isfirst)
                    controler =  1 - controler;
                location = packet.ReadByte();
                sequence = packet.ReadInt32();
                position = packet.ReadInt32();
            }
        }

        public GameBehavior(GameClient game)
        {
            Game = game;
            Connection = game.Connection;
            _hand = game.Hand;
            _debug = game.Debug;
            _packets = new Dictionary<StocMessage, Action<BinaryReader>>();
            _messages = new Dictionary<GameMessage, Action<BinaryReader>>();
            RegisterPackets();

            _room = new Room();
            _duel = new Duel();

            _ai = new GameAI(_duel, Game.Dialog, Game.Chat, Game.Log, Program.AssetPath);
            
            // First load the custom deck if provided
            if (Game.DeckFile != null)
            {
                Logger.WriteLine("Custom deck provided, loading: " + Game.DeckFile);
                Deck = Deck.Load(Game.DeckFile);
                // Use Generic executor for custom decks
                _ai.Executor = DecksManager.InstantiateGeneric(_ai, _duel);
            }
            else
            {
                // Fall back to built-in deck and executor
                _ai.Executor = DecksManager.Instantiate(_ai, _duel, Game.Deck);
                Deck = Deck.Load(_ai.Executor.Deck);
            }

            _select_hint = 0;
        }

        public int GetLocalPlayer(int player)
        {
            return _duel.IsFirst ? player : 1 - player;
        }

        public void OnPacket(BinaryReader packet)
        {
            StocMessage id = (StocMessage)packet.ReadByte();
            if (id == StocMessage.GameMsg)
            {
                GameMessage msg = (GameMessage)packet.ReadByte();
                if (_messages.ContainsKey(msg))
                    _messages[msg](packet);
                _lastMessage = msg;
                return;
            }
            if (_packets.ContainsKey(id))
                _packets[id](packet);
        }

        private void RegisterPackets()
        {
            _packets.Add(StocMessage.JoinGame, OnJoinGame);
            _packets.Add(StocMessage.TypeChange, OnTypeChange);
            _packets.Add(StocMessage.HsPlayerEnter, OnPlayerEnter);
            _packets.Add(StocMessage.HsPlayerChange, OnPlayerChange);
            _packets.Add(StocMessage.SelectHand, OnSelectHand);
            _packets.Add(StocMessage.SelectTp, OnSelectTp);
            _packets.Add(StocMessage.TimeLimit, OnTimeLimit);
            _packets.Add(StocMessage.Replay, OnReplay);
            _packets.Add(StocMessage.DuelEnd, OnDuelEnd);
            _packets.Add(StocMessage.Chat, OnChat);
            _packets.Add(StocMessage.ChangeSide, OnChangeSide);
            _packets.Add(StocMessage.ErrorMsg, OnErrorMsg);
            _packets.Add(StocMessage.Rematch, OnRematch);

            _messages.Add(GameMessage.Retry, OnRetry);
            _messages.Add(GameMessage.Start, OnStart);
            _messages.Add(GameMessage.Hint, OnHint);
            _messages.Add(GameMessage.Win, OnWin);
            _messages.Add(GameMessage.Draw, OnDraw);
            _messages.Add(GameMessage.ShuffleDeck, OnShuffleDeck);
            _messages.Add(GameMessage.ShuffleHand, OnShuffleHand);
            _messages.Add(GameMessage.ShuffleExtra, OnShuffleExtra);
            _messages.Add(GameMessage.SwapGraveDeck, OnSwapGraveDeck);
            _messages.Add(GameMessage.ShuffleSetCard, OnShuffleSetCard);
            _messages.Add(GameMessage.TagSwap, OnTagSwap);
            _messages.Add(GameMessage.ReloadField, OnReloadField);
            _messages.Add(GameMessage.NewTurn, OnNewTurn);
            _messages.Add(GameMessage.NewPhase, OnNewPhase);
            _messages.Add(GameMessage.Damage, OnDamage);
            _messages.Add(GameMessage.PayLpCost, OnDamage);
            _messages.Add(GameMessage.Recover, OnRecover);
            _messages.Add(GameMessage.LpUpdate, OnLpUpdate);
            _messages.Add(GameMessage.Move, OnMove);
            _messages.Add(GameMessage.Swap, OnSwap);
            _messages.Add(GameMessage.Attack, OnAttack);
            _messages.Add(GameMessage.Battle, OnBattle);
            _messages.Add(GameMessage.AttackDisabled, OnAttackDisabled);
            _messages.Add(GameMessage.PosChange, OnPosChange);
            _messages.Add(GameMessage.Chaining, OnChaining);
            _messages.Add(GameMessage.ChainEnd, OnChainEnd);
            _messages.Add(GameMessage.SortCard, OnCardSorting);
            _messages.Add(GameMessage.SortChain, OnChainSorting);
            _messages.Add(GameMessage.UpdateCard, OnUpdateCard);
            _messages.Add(GameMessage.UpdateData, OnUpdateData);
            _messages.Add(GameMessage.BecomeTarget, OnBecomeTarget);
            _messages.Add(GameMessage.SelectBattleCmd, OnSelectBattleCmd);
            _messages.Add(GameMessage.SelectCard, OnSelectCard);
            _messages.Add(GameMessage.SelectUnselect, OnSelectUnselectCard);
            _messages.Add(GameMessage.SelectChain, OnSelectChain);
            _messages.Add(GameMessage.SelectCounter, OnSelectCounter);
            _messages.Add(GameMessage.SelectDisfield, OnSelectDisfield);
            _messages.Add(GameMessage.SelectEffectYn, OnSelectEffectYn);
            _messages.Add(GameMessage.SelectIdleCmd, OnSelectIdleCmd);
            _messages.Add(GameMessage.SelectOption, OnSelectOption);
            _messages.Add(GameMessage.SelectPlace, OnSelectPlace);
            _messages.Add(GameMessage.SelectPosition, OnSelectPosition);
            _messages.Add(GameMessage.SelectSum, OnSelectSum);
            _messages.Add(GameMessage.SelectTribute, OnSelectTribute);
            _messages.Add(GameMessage.SelectYesNo, OnSelectYesNo);
            _messages.Add(GameMessage.AnnounceAttrib, OnAnnounceAttrib);
            _messages.Add(GameMessage.AnnounceCard, OnAnnounceCard);
            _messages.Add(GameMessage.AnnounceNumber, OnAnnounceNumber);
            _messages.Add(GameMessage.AnnounceRace, OnAnnounceRace);
            _messages.Add(GameMessage.RockPaperScissors, OnRockPaperScissors);
            _messages.Add(GameMessage.Equip, OnEquip);
            _messages.Add(GameMessage.Unequip, OnUnEquip);
            _messages.Add(GameMessage.CardTarget, OnCardTarget);
            _messages.Add(GameMessage.CancelTarget, OnCancelTarget);
            _messages.Add(GameMessage.Summoning, OnSummoning);
            _messages.Add(GameMessage.Summoned, OnSummoned);
            _messages.Add(GameMessage.SpSummoning, OnSpSummoning);
            _messages.Add(GameMessage.SpSummoned, OnSpSummoned);
            _messages.Add(GameMessage.FlipSummoning, OnSummoning);
            _messages.Add(GameMessage.FlipSummoned, OnSummoned);
        }

        private void OnJoinGame(BinaryReader packet)
        {
            /*int lflist = (int)*/ packet.ReadUInt32();
            /*int rule = */ packet.ReadByte();
            /*int mode = */ packet.ReadByte();
            int duel_rule = packet.ReadByte();
            /*bool nocheck deck =*/ packet.ReadByte();
            /*bool noshuffle deck =*/ packet.ReadByte();
            /*align*/ packet.ReadBytes(3);
            /*int start_lp =(int)*/ packet.ReadUInt32();
            /*int start_hand =*/ packet.ReadByte();
            /*int draw_count =*/ packet.ReadByte();
            /*int time_limit =*/ packet.ReadUInt16();
            /*align =*/ packet.ReadBytes(4);
            const uint SERVER_HANDSHAKE = 4043399681u;
            uint handshake = packet.ReadUInt32();
            if (handshake != SERVER_HANDSHAKE)
            {
                Connection.Close();
                return;
            }
            /*int version =*/ packet.ReadUInt32();
            int team1 = packet.ReadInt32();
            int team2 = packet.ReadInt32();
            /*int best_of =*/ packet.ReadInt32();
            int duel_flag = packet.ReadInt32();
            /*int forbidden_types =*/ packet.ReadInt32();
            /*int extra_rules =*/ packet.ReadInt32();
            _room.Players = team1 + team2;
            const int DUEL_EMZONE = 0x2000;
            const int DUEL_FSX_MMZONE = 0x4000;
            _ai.Duel.IsNewRule = (duel_flag & DUEL_EMZONE) != 0;
            _ai.Duel.IsNewRule2020 = (duel_flag & DUEL_FSX_MMZONE) != 0;
            BinaryWriter deck = GamePacketFactory.Create(CtosMessage.UpdateDeck);
            deck.Write(Deck.Cards.Count + Deck.ExtraCards.Count);
            deck.Write(Deck.SideCards.Count);
            foreach (int card in Deck.Cards)
                deck.Write(card);
            foreach (int card in Deck.ExtraCards)
                deck.Write(card);
            foreach (int card in Deck.SideCards)
                deck.Write(card);
            Connection.Send(deck);
            _ai.OnJoinGame();
        }

        private void OnChangeSide(BinaryReader packet)
        {
            BinaryWriter deck = GamePacketFactory.Create(CtosMessage.UpdateDeck);
            deck.Write(Deck.Cards.Count + Deck.ExtraCards.Count);
            deck.Write(Deck.SideCards.Count);
            foreach (int card in Deck.Cards)
                deck.Write(card);
            foreach (int card in Deck.ExtraCards)
                deck.Write(card);
            foreach (int card in Deck.SideCards)
                deck.Write(card);
            Connection.Send(deck);
            _ai.OnJoinGame();
        }

        private void OnRematch(BinaryReader packet)
        {
            Connection.Send(CtosMessage.RematchResponse, (byte)(1));
        }

        private void OnTypeChange(BinaryReader packet)
        {
            int type = packet.ReadByte();
            int pos = type & 0xF;
            if (pos < 0 || pos >= _room.Players)
            {
                Connection.Close();
                return;
            }
            _room.Position = pos;
            _room.IsHost = ((type >> 4) & 0xF) != 0;
            _room.IsReady[pos] = true;
            Connection.Send(CtosMessage.HsReady);
        }

        private void OnPlayerEnter(BinaryReader packet)
        {
            string name = packet.ReadUnicode(20);
            int pos = packet.ReadByte();
            if (pos < 8)
                _room.Names[pos] = name;
        }

        private void OnPlayerChange(BinaryReader packet)
        {
            int change = packet.ReadByte();
            int pos = (change >> 4) & 0xF;
            int state = change & 0xF;
            if (pos >= _room.Players)
                return;
            if (state < 8)
            {
                string oldname = _room.Names[pos];
                _room.Names[pos] = null;
                _room.Names[state] = oldname;
                _room.IsReady[pos] = false;
                _room.IsReady[state] = false;
            }
            else if (state == (int)PlayerChange.Ready)
                _room.IsReady[pos] = true;
            else if (state == (int)PlayerChange.NotReady)
                _room.IsReady[pos] = false;
            else if (state == (int)PlayerChange.Leave || state == (int)PlayerChange.Observe)
            {
                _room.IsReady[pos] = false;
                _room.Names[pos] = null;
            }

            if (_room.IsHost && _room.IsReady[0] && _room.IsReady[1])
                Connection.Send(CtosMessage.HsStart);
        }

        private void OnSelectHand(BinaryReader packet)
        {
            int result;
            if (_hand > 0)
                result = _hand;
            else
                result = _ai.OnRockPaperScissors();
            Connection.Send(CtosMessage.HandResult, (byte)result);
        }

        private void OnSelectTp(BinaryReader packet)
        {
            bool start = _ai.OnSelectHand();
            Connection.Send(CtosMessage.TpResult, (byte)(start ? 1 : 0));
        }

        private void OnTimeLimit(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            if (player == 0)
                Connection.Send(CtosMessage.TimeConfirm);
        }

        private void OnReplay(BinaryReader packet)
        {
            /*byte[] replay =*/ packet.ReadToEnd();

            /*
            const string directory = "Replays";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string otherName = _room.Position == 0 ? _room.Names[1] : _room.Names[0];
            string file = DateTime.Now.ToString("yyyy-MM-dd.HH-mm.") + otherName + ".yrp";
            string fullname = Path.Combine(directory, file);

            if (Regex.IsMatch(file, @"^[\w\-. ]+$"))
                File.WriteAllBytes(fullname, replay);
            */

            //Connection.Close();
        }

        private void OnDuelEnd(BinaryReader packet)
        {
            Logger.DebugWriteLine("Duel ended.");
        }

        private void OnChat(BinaryReader packet)
        {
            int player = packet.ReadInt16();
            string message = packet.ReadUnicode(256);
            string myName = (player != 0) ? _room.Names[1] : _room.Names[0];
            string otherName = (player == 0) ? _room.Names[1] : _room.Names[0];
            if (player < 4)
                Logger.DebugWriteLine(otherName + " say to " + myName + ": " + message);
        }

        private void OnErrorMsg(BinaryReader packet)
        {
            int msg = packet.ReadByte();
            // align
            packet.ReadByte();
            packet.ReadByte();
            packet.ReadByte();
            if (msg == 2) //ERRMSG_DECKERROR
            {
                int flag = packet.ReadInt32();
                packet.ReadInt32();
                packet.ReadInt32();
                packet.ReadInt32();
                int code = packet.ReadInt32();
                if (flag <= 5) //DECKERROR_CARDCOUNT
                {
                    NamedCard card = NamedCard.Get(code);
                    if (card != null)
                        _ai.OnDeckError(card.Name);
                    else
                        _ai.OnDeckError("Unknown Card");
                }
                else
                    _ai.OnDeckError("DECK");
            }
            Logger.WriteErrorLine("Error message received: " + msg);
        }

        private void OnRetry(BinaryReader packet)
        {
            string otherName = _room.Position == 0 ? _room.Names[1] : _room.Names[0];
            Logger.DebugWriteLine("Duel finished against " + otherName + " because of MsgRetry");
        }

        private void OnHint(BinaryReader packet)
        {
            int type = packet.ReadByte();
            int player = packet.ReadByte();
            long data = packet.ReadInt64();
            if (type == 1) // HINT_EVENT
            {
                if (data == 24) // battling
                {
                    _duel.Fields[0].UnderAttack = false;
                    _duel.Fields[1].UnderAttack = false;
                } else if (data == 23) //Main Phase end
                {
                    _duel.MainPhaseEnd = true;
                }
            }
            if (type == 3) // HINT_SELECTMSG
            {
                _select_hint = data;
            }
        }

        private void OnStart(BinaryReader packet)
        {
            int type = packet.ReadByte();
            _duel.IsFirst = (type & 0xF) == 0;
            _duel.Turn = 0;
            /*int duel_rule = packet.ReadByte();
            _ai.Duel.IsNewRule = (duel_rule == 4);
            _ai.Duel.IsNewRule2020 = (duel_rule >= 5);*/
            _duel.Fields[GetLocalPlayer(0)].LifePoints = packet.ReadInt32();
            _duel.Fields[GetLocalPlayer(1)].LifePoints = packet.ReadInt32();
            int deck = packet.ReadInt16();
            int extra = packet.ReadInt16();
            _duel.Fields[GetLocalPlayer(0)].Init(deck, extra);
            deck = packet.ReadInt16();
            extra = packet.ReadInt16();
            _duel.Fields[GetLocalPlayer(1)].Init(deck, extra);

            Logger.DebugWriteLine("Duel started: " + _room.Names[0] + " versus " + _room.Names[1]);
            _ai.OnStart();
        }

        private void OnWin(BinaryReader packet)
        {
            int result = GetLocalPlayer(packet.ReadByte());

            string otherName = _room.Position == 0 ? _room.Names[1] : _room.Names[0];
            string textResult = (result == 2 ? "Draw" : result == 0 ? "Win" : "Lose");
            Logger.DebugWriteLine("Duel finished against " + otherName + ", result: " + textResult);
        }

        private void OnDraw(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            int count = packet.ReadInt32();
            if (_debug)
                Logger.WriteLine("(" + player.ToString() + " draw " + count.ToString() + " card)");

            for (int i = 0; i < count; ++i)
            {
                _duel.Fields[player].Deck.RemoveAt(_duel.Fields[player].Deck.Count - 1);
                _duel.Fields[player].Hand.Add(new ClientCard(0, CardLocation.Hand, -1, player));
            }
            _ai.OnDraw(player);
        }

        private void OnShuffleDeck(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            foreach (ClientCard card in _duel.Fields[player].Deck)
                card.SetId(0);
        }

        private void OnShuffleHand(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            packet.ReadInt32();
            foreach (ClientCard card in _duel.Fields[player].Hand)
                card.SetId(packet.ReadInt32());
        }

        private void OnShuffleExtra(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            packet.ReadInt32();
            foreach (ClientCard card in _duel.Fields[player].ExtraDeck)
            {
                if (!card.IsFaceup())
                    card.SetId(packet.ReadInt32());
            }
        }

        private void OnSwapGraveDeck(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            int extra_insert_off = packet.ReadInt32();
            int extra_buffer_size = packet.ReadInt32();
            BitArray extra_buffer = new BitArray(packet.ReadBytes(extra_buffer_size));
            _duel.Fields[player].SwapDeckAndGrave(extra_buffer, extra_insert_off);
        }

        private void OnShuffleSetCard(BinaryReader packet)
        {
            int location = packet.ReadByte();
            int count = packet.ReadByte();
            ClientCard[] list = new ClientCard[5];
            for (int i = 0; i < count; ++i)
            {
                LocationInfo loc = new LocationInfo(packet, _duel.IsFirst);
                ClientCard card = _duel.GetCard(loc.controler, (CardLocation)loc.location, loc.sequence);
                if (card == null) continue;
                list[i] = card;
                card.SetId(0);
            }
            for (int i = 0; i < count; ++i)
            {
                LocationInfo loc = new LocationInfo(packet, _duel.IsFirst);
                ClientCard card = _duel.GetCard(loc.controler, (CardLocation)loc.location, loc.sequence);
                if (card == null) continue;
                ClientCard[] zone = (loc.location == (int)CardLocation.MonsterZone) ? _duel.Fields[loc.controler].MonsterZone : _duel.Fields[loc.controler].SpellZone;
                zone[loc.sequence] = list[i];
            }
        }

        private void OnTagSwap(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            int mcount = packet.ReadInt32();
            int ecount = packet.ReadInt32();
            /*int pcount = */ packet.ReadInt32();
            int hcount = packet.ReadInt32();
            /*int topcode =*/ packet.ReadInt32();
            _duel.Fields[player].Deck.Clear();
            for (int i = 0; i < mcount; ++i)
            {
                _duel.Fields[player].Deck.Add(new ClientCard(0, CardLocation.Deck, -1, player));
            }
            _duel.Fields[player].ExtraDeck.Clear();
            for (int i = 0; i < ecount; ++i)
            {
                int code = packet.ReadInt32();
                _duel.Fields[player].ExtraDeck.Add(new ClientCard(code, CardLocation.Extra, -1, player));
                packet.ReadInt32(); // position
            }
            _duel.Fields[player].Hand.Clear();
            for (int i = 0; i < hcount; ++i)
            {
                int code = packet.ReadInt32();
                _duel.Fields[player].Hand.Add(new ClientCard(code, CardLocation.Hand,-1, player));
                packet.ReadInt32(); // position
            }
        }

        private void OnReloadField(BinaryReader packet)
        {
            /*int opts = */ packet.ReadInt32();
            _duel.Clear();
            for (int player = 0; player < 2; player++)
            {
                int i = GetLocalPlayer(player);
                _duel.Fields[i].LifePoints = packet.ReadInt32();
                for (int seq = 0; seq < 7; ++seq)
                {
                    if (packet.ReadByte() == 0)
                        continue;
                    int position = packet.ReadByte();
                    _duel.AddCard(CardLocation.MonsterZone, 0, i, seq, position);
                    var card = _duel.GetCard(i, CardLocation.MonsterZone, seq);
                    int overlay_count = packet.ReadInt32();
                    for (int xyz = 0; xyz < overlay_count; ++xyz)
                    {
                        card.Overlays.Add(0);
                    }
                }
                for (int seq = 0; seq < 8; ++seq)
                {
                    if (packet.ReadByte() == 0)
                        continue;
                    int position = packet.ReadByte();
                    _duel.AddCard(CardLocation.SpellZone, 0, i, seq, position);
                    var card = _duel.GetCard(i, CardLocation.SpellZone, seq);
                    int overlay_count = packet.ReadInt32();
                    for (int xyz = 0; xyz < overlay_count; ++xyz)
                    {
                        card.Overlays.Add(0);
                    }
                }
                int deck_size = packet.ReadInt32();
                for(int seq = 0; seq < deck_size; ++seq)
                    _duel.AddCard(CardLocation.Deck, 0, i, seq, (int)CardPosition.FaceDown);
                int hand_size = packet.ReadInt32();
                for(int seq = 0; seq < hand_size; ++seq)
                    _duel.AddCard(CardLocation.Hand, 0, i, seq, (int)CardPosition.FaceDown);
                int grave_size = packet.ReadInt32();
                for(int seq = 0; seq < grave_size; ++seq)
                    _duel.AddCard(CardLocation.Grave, 0, i, seq, (int)CardPosition.FaceDown);
                int removed_size = packet.ReadInt32();
                for(int seq = 0; seq < removed_size; ++seq)
                    _duel.AddCard(CardLocation.Removed, 0, i, seq, (int)CardPosition.FaceDown);
                int extra_deck_size = packet.ReadInt32();
                for(int seq = 0; seq < extra_deck_size; ++seq)
                    _duel.AddCard(CardLocation.Extra, 0, i, seq, (int)CardPosition.FaceDown);
                /*int extra_p_count = */ packet.ReadInt32();
            }
            int chain_count = packet.ReadInt32();
            for (int i = 0; i < chain_count; i++)
            {
                int cardId = packet.ReadInt32();
                LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
                int chain_player = packet.ReadByte();
                /*int chain_location = */packet.ReadByte();
                /*int chain_sequence = */packet.ReadInt32();
                /*long chain_description = */packet.ReadInt64();
                _duel.LastChainPlayer = chain_player;
                ClientCard card = _duel.GetCard(info.controler, info.location, info.sequence, info.position);
                if (card.Id == 0)
                    card.SetId(cardId);
                _duel.CurrentChain.Add(card);
            }
        }

        private void OnNewTurn(BinaryReader packet)
        {
            _duel.Turn++;
            _duel.Player = GetLocalPlayer(packet.ReadByte());
            _ai.OnNewTurn();
        }

        private void OnNewPhase(BinaryReader packet)
        {
            _duel.Phase = (DuelPhase)packet.ReadInt16();
            if (_debug && _duel.Phase == DuelPhase.Standby)
            {
                Logger.WriteLine("*********Bot Hand*********");
                foreach (ClientCard card in _duel.Fields[0].Hand)
                {
                    if (card != null)
                    {
                        Logger.WriteLine(card.Name);
                    }
                }
                Logger.WriteLine("*********Bot Spell*********");
                foreach (ClientCard card in _duel.Fields[0].SpellZone)
                {
                    if (card != null)
                    {
                        Logger.WriteLine(card.Name);
                    }
                }
                Logger.WriteLine("*********Bot Monster*********");
                foreach (ClientCard card in _duel.Fields[0].MonsterZone)
                {
                    if (card != null)
                    {
                        Logger.WriteLine(card.Name);
                    }
                }
                Logger.WriteLine("*********Finish*********");
            }
            if (_debug)
                Logger.WriteLine("(Go to " + (_duel.Phase.ToString()) + ")");
            _duel.LastSummonPlayer = -1;
            _duel.SummoningCards.Clear();
            _duel.LastSummonedCards.Clear();
            _duel.Fields[0].BattlingMonster = null;
            _duel.Fields[1].BattlingMonster = null;
            _duel.Fields[0].UnderAttack = false;
            _duel.Fields[1].UnderAttack = false;
            List<ClientCard> monsters = _duel.Fields[0].GetMonsters();
            foreach (ClientCard monster in monsters)
            {
                monster.Attacked = false;
            }
            _duel.MainPhaseEnd = false;
            _select_hint = 0;
            _ai.OnNewPhase();
        }

        private void OnDamage(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            int final = _duel.Fields[player].LifePoints - packet.ReadInt32();
            if (final < 0) final = 0;
            if (_debug)
                Logger.WriteLine("(" + player.ToString() + " got damage , LifePoint left = " + final.ToString() + ")");
            _duel.Fields[player].LifePoints = final;
        }

        private void OnRecover(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            int final = _duel.Fields[player].LifePoints + packet.ReadInt32();
            if (_debug)
                Logger.WriteLine("(" + player.ToString() + " got healed , LifePoint left = " + final.ToString() + ")");
            _duel.Fields[player].LifePoints = final;
        }

        private void OnLpUpdate(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            _duel.Fields[player].LifePoints = packet.ReadInt32();
        }

        private void OnMove(BinaryReader packet)
        {
            // TODO: update equip cards and target cards
            int cardId = packet.ReadInt32();
            LocationInfo previous = new LocationInfo(packet, _duel.IsFirst);
            LocationInfo current = new LocationInfo(packet, _duel.IsFirst);
            packet.ReadInt32(); // reason

            ClientCard card = _duel.GetCard(previous.controler, (CardLocation)previous.location, previous.sequence);
            if ((previous.location & (int)CardLocation.Overlay) != 0)
            {
                previous.location = previous.location & 0x7f;
                card = _duel.GetCard(previous.controler, (CardLocation)previous.location, previous.sequence);
                if (card != null)
                {
                    if (_debug)
                    {
                        NamedCard cardName = NamedCard.Get(cardId);
                        string cardNameStr = cardName != null ? cardName.Name : "UnknownCard";
                        Logger.WriteLine("(" + previous.controler.ToString() + " 's " + (card != null && card.Name != null ? card.Name : "UnKnowCard") + " deattach " + cardNameStr + ")");
                    }
                    card.Overlays.Remove(cardId);
                }
                previous.location = 0; // the card is removed when it go to overlay, so here we treat it as a new card
            }
            else
                _duel.RemoveCard((CardLocation)previous.location, card, previous.controler, previous.sequence);

            if ((current.location & (int)CardLocation.Overlay) != 0)
            {
                current.location = current.location & 0x7f;
                card = _duel.GetCard(current.controler, (CardLocation)current.location, current.sequence);
                if (card != null)
                {
                    if (_debug)
                    {
                        NamedCard cardName = NamedCard.Get(cardId);
                        string cardNameStr = cardName != null ? cardName.Name : "UnknownCard";
                        Logger.WriteLine("(" + previous.controler.ToString() + " 's " + (card != null && card.Name != null ? card.Name : "UnKnowCard") + " overlay " + cardNameStr + ")");
                    }
                    card.Overlays.Add(cardId);
                }
            }
            else
            {
                if (previous.location == 0)
                {
                    if (_debug)
                    {
                        NamedCard cardName = NamedCard.Get(cardId);
                        string cardNameStr = cardName != null ? cardName.Name : "UnknownCard";
                        Logger.WriteLine("(" + previous.controler.ToString() + " 's " + cardNameStr
                        + " appear in " + (CardLocation)current.location + ")");
                    }
                    _duel.AddCard((CardLocation)current.location, cardId, current.controler, current.sequence, current.position);
                }
                else
                {
                    _duel.AddCard((CardLocation)current.location, card, current.controler, current.sequence, current.position, cardId);
                    if (card != null && previous.location != current.location)
                        card.IsSpecialSummoned = false;
                    if (_debug && card != null)
                        Logger.WriteLine("(" + previous.controler.ToString() + " 's " + (card != null && card.Name != null ? card.Name : "UnKnowCard")
                        + " from " +
                        (CardLocation)previous.location + " move to " + (CardLocation)current.location + ")");
                }
            }
        }

        private void OnSwap(BinaryReader packet)
        {
            int cardId1 = packet.ReadInt32();
            LocationInfo info1 = new LocationInfo(packet, _duel.IsFirst);
            int cardId2 = packet.ReadInt32();
            LocationInfo info2 = new LocationInfo(packet, _duel.IsFirst);
            ClientCard card1 = _duel.GetCard(info1.controler, (CardLocation)info1.location, info1.sequence);
            ClientCard card2 = _duel.GetCard(info2.controler, (CardLocation)info2.location, info2.sequence);
            if (card1 == null || card2 == null) return;
            _duel.RemoveCard((CardLocation)info1.location, card1, info1.controler, info1.sequence);
            _duel.RemoveCard((CardLocation)info2.location, card2, info2.controler, info2.sequence);
            _duel.AddCard((CardLocation)info2.location, card1, info2.controler, info2.sequence, card1.Position, cardId1);
            _duel.AddCard((CardLocation)info1.location, card2, info1.controler, info1.sequence, card2.Position, cardId2);
        }

        private void OnAttack(BinaryReader packet)
        {
            LocationInfo info1 = new LocationInfo(packet, _duel.IsFirst);
            LocationInfo info2 = new LocationInfo(packet, _duel.IsFirst);

            ClientCard attackcard = _duel.GetCard(info1.controler, (CardLocation)info1.location, info1.sequence);
            ClientCard defendcard = _duel.GetCard(info2.controler, (CardLocation)info2.location, info2.sequence);
            if (_debug)
            {
                if (defendcard == null) Logger.WriteLine("(" + (attackcard != null && attackcard.Name != null ? attackcard.Name : "UnKnowCard") + " direct attack!!)");
                else Logger.WriteLine("(" + info1.controler.ToString() + " 's " + (attackcard != null && attackcard.Name != null ? attackcard.Name : "UnKnowCard") + " attack  " + info2.controler.ToString() + " 's " + (defendcard != null && defendcard.Name != null ? defendcard.Name : "UnKnowCard") + ")");
            }
            _duel.Fields[attackcard.Controller].BattlingMonster = attackcard;
            _duel.Fields[1 - attackcard.Controller].BattlingMonster = defendcard;
            _duel.Fields[1 - attackcard.Controller].UnderAttack = true;

            if (info2.location == 0 && info1.controler != 0)
            {
                _ai.OnDirectAttack(attackcard);
            }
        }

        private void OnBattle(BinaryReader packet)
        {
            _duel.Fields[0].UnderAttack = false;
            _duel.Fields[1].UnderAttack = false;
        }

        private void OnAttackDisabled(BinaryReader packet)
        {
            _duel.Fields[0].UnderAttack = false;
            _duel.Fields[1].UnderAttack = false;
        }

        private void OnPosChange(BinaryReader packet)
        {
            packet.ReadInt32(); // card id
            int pc = GetLocalPlayer(packet.ReadByte());
            int pl = packet.ReadByte();
            int ps = packet.ReadSByte();
            int pp = packet.ReadSByte();
            int cp = packet.ReadSByte();
            ClientCard card = _duel.GetCard(pc, (CardLocation)pl, ps);
            if (card != null)
            {
                card.Position = cp;
                if ((pp & (int) CardPosition.FaceUp) > 0 && (cp & (int) CardPosition.FaceDown) > 0)
                    card.ClearCardTargets();
                if (_debug)
                    Logger.WriteLine("(" + (card != null && card.Name != null ? card.Name : "UnKnowCard") + " change position to " + (CardPosition)cp + ")");
            }
        }

        private void OnChaining(BinaryReader packet)
        {
            int cardId = packet.ReadInt32();
            LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
            ClientCard card = _duel.GetCard(info.controler, info.location, info.sequence, info.position);
            if (card.Id == 0)
                card.SetId(cardId);
            int cc = GetLocalPlayer(packet.ReadByte());
            if (_debug)
                if (card != null) Logger.WriteLine("(" + cc.ToString() + " 's " + (card != null && card.Name != null ? card.Name : "UnKnowCard") + " activate effect)");
            
            // Call AI's OnChaining with the card and player
            _ai.OnChaining(card, cc);
            
            _duel.ChainTargetOnly.Clear();
            _duel.LastSummonPlayer = -1;
            _duel.CurrentChain.Add(card);
            _duel.LastChainPlayer = cc;
        }

        private void OnChainEnd(BinaryReader packet)
        {
            if (_debug)
                Logger.WriteLine("Chain finished");
            
            // Call AI's OnChainEnd
            _ai.OnChainEnd();
            
            _duel.CurrentChain.Clear();
            _duel.LastChainPlayer = -1;
        }

        private void OnCardSorting(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            IList<ClientCard> originalCards = new List<ClientCard>();
            IList<ClientCard> cards = new List<ClientCard>();
            int count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int id = packet.ReadInt32();
                LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
                ClientCard card;
                if (((int)info.location & (int)CardLocation.Overlay) != 0)
                    card = new ClientCard(id, CardLocation.Overlay, -1, player);
                else if (info.location == 0)
                    card = new ClientCard(id, 0, 0, player);
                else
                    card = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
                if (card == null) continue;
                if (card.Id == 0)
                    card.SetId(id);
                originalCards.Add(card);
                cards.Add(card);
            }

            IList<ClientCard> selected = _ai.OnCardSorting(cards);
            byte[] result = new byte[count];
            for (int i = 0; i < count; ++i)
            {
                int id = 0;
                for (int j = 0; j < count; ++j)
                {
                    if (selected[j] == null) continue;
                    if (selected[j].Equals(originalCards[i]))
                    {
                        id = j;
                        break;
                    }
                }
                result[i] = (byte)id;
            }

            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);
            reply.Write(result);
            Connection.Send(reply);
        }

        private void OnChainSorting(BinaryReader packet)
        {
            /*BinaryWriter writer =*/ GamePacketFactory.Create(CtosMessage.Response);
            Connection.Send(CtosMessage.Response, -1);
        }

        private void OnUpdateCard(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            int loc = packet.ReadByte();
            int seq = packet.ReadByte();

            ClientCard card = _duel.GetCard(player, (CardLocation)loc, seq);

            if (card != null)
            {
                card.Update(packet, _duel);
            }
        }

        private void OnUpdateData(BinaryReader packet)
        {
            int player = GetLocalPlayer(packet.ReadByte());
            CardLocation loc = (CardLocation)packet.ReadByte();
            IList<ClientCard> cards = null;
            switch (loc)
            {
                case CardLocation.Hand:
                    cards = _duel.Fields[player].Hand;
                    break;
                case CardLocation.MonsterZone:
                    cards = _duel.Fields[player].MonsterZone;
                    break;
                case CardLocation.SpellZone:
                    cards = _duel.Fields[player].SpellZone;
                    break;
                case CardLocation.Grave:
                    cards = _duel.Fields[player].Graveyard;
                    break;
                case CardLocation.Removed:
                    cards = _duel.Fields[player].Banished;
                    break;
                case CardLocation.Deck:
                    cards = _duel.Fields[player].Deck;
                    break;
                case CardLocation.Extra:
                    cards = _duel.Fields[player].ExtraDeck;
                    break;
            }
            if (cards != null)
            {
                /*int size = */packet.ReadInt32();
                foreach (ClientCard card in cards)
                {
                    if (card != null)
                    {
                        long pos = packet.BaseStream.Position;
                        long len = card.Update(packet, _duel);
                        packet.BaseStream.Position = pos + len;
                    }
                    else
                    {
                        packet.BaseStream.Position += 2;
                    }
                }
            }
        }

        private void OnBecomeTarget(BinaryReader packet)
        {
            int count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
                ClientCard card = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
                if (card == null) continue;
                if (_debug)
                    Logger.WriteLine("(" + (CardLocation)info.location + " 's " + (card != null && card.Name != null ? card.Name : "UnKnowCard") + " become target)");
                _duel.ChainTargets.Add(card);
                _duel.ChainTargetOnly.Add(card);
            }
        }

        private void OnSelectBattleCmd(BinaryReader packet)
        {
            packet.ReadByte(); // player
            _duel.BattlePhase = new BattlePhase();
            BattlePhase battle = _duel.BattlePhase;

            int count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                packet.ReadInt32(); // card id
                int con = GetLocalPlayer(packet.ReadByte());
                CardLocation loc = (CardLocation)packet.ReadByte();
                int seq = packet.ReadInt32();
                long desc = packet.ReadInt64();
                packet.ReadByte(); // operation type

                ClientCard card = _duel.GetCard(con, loc, seq);
                if (card != null)
                {
                    card.ActionIndex[0] = i;
                    battle.ActivableCards.Add(card);
                    battle.ActivableDescs.Add(desc);
                }
            }

            count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                packet.ReadInt32(); // card id
                int con = GetLocalPlayer(packet.ReadByte());
                CardLocation loc = (CardLocation)packet.ReadByte();
                int seq = packet.ReadByte();
                int diratt = packet.ReadByte();

                ClientCard card = _duel.GetCard(con, loc, seq);
                if (card != null)
                {
                    card.ActionIndex[1] = i;
                    if (diratt > 0)
                        card.CanDirectAttack = true;
                    else
                        card.CanDirectAttack = false;
                    battle.AttackableCards.Add(card);
                    card.Attacked = false;
                }
            }
            List<ClientCard> monsters = _duel.Fields[0].GetMonsters();
            foreach (ClientCard monster in monsters)
            {
                if (!battle.AttackableCards.Contains(monster))
                    monster.Attacked = true;
            }

            battle.CanMainPhaseTwo = packet.ReadByte() != 0;
            battle.CanEndPhase = packet.ReadByte() != 0;

            Connection.Send(CtosMessage.Response, _ai.OnSelectBattleCmd(battle).ToValue());
        }

        private void InternalOnSelectCard(BinaryReader packet, Func<IList<ClientCard>, int, int, long, bool, IList<ClientCard>> func, bool tribute = false)
        {
            int player = packet.ReadByte();
            bool cancelable = packet.ReadByte() != 0;
            int min = packet.ReadInt32();
            int max = packet.ReadInt32();

            IList<ClientCard> cards = new List<ClientCard>();
            int count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int id = packet.ReadInt32();
                LocationInfo info = !tribute ? new LocationInfo(packet, _duel.IsFirst) : new LocationInfo();
                if (tribute)
                {
                    info.controler = packet.ReadByte();
                    if (!_duel.IsFirst)
                        info.controler = 1 - info.controler;
                    info.location = packet.ReadByte();
                    info.sequence = packet.ReadInt32();
                    packet.ReadByte();
                }
                ClientCard card;
                if (((int)info.location & (int)CardLocation.Overlay) != 0)
                    card = new ClientCard(id, CardLocation.Overlay, -1, player);
                else if (info.location == 0)
                    card = new ClientCard(id, 0, 0, player);
                else
                    card = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
                if (card == null) continue;
                if (card.Id == 0)
                    card.SetId(id);
                cards.Add(card);
            }

            IList<ClientCard> selected = func(cards, min, max, _select_hint, cancelable);
            _select_hint = 0;

            if (selected.Count == 0 && cancelable)
            {
                Connection.Send(CtosMessage.Response, -1);
                return;
            }

            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);

            reply.Write((int)0);
            reply.Write((int)selected.Count);

            for (int i = 0; i < selected.Count; ++i)
            {
                int id = 0;
                for (int j = 0; j < count; ++j)
                {
                    if (cards[j] == null) continue;
                    if (cards[j].Equals(selected[i]))
                    {
                        id = j;
                        break;
                    }
                }
                reply.Write((int)id);
            }
            Connection.Send(reply);
        }

        private void InternalOnSelectUnselectCard(BinaryReader packet, Func<IList<ClientCard>, int, int, long, bool, IList<ClientCard>> func)
        {
            int player = packet.ReadByte();
            bool finishable = packet.ReadByte() != 0;
            bool cancelable = packet.ReadByte() != 0 || finishable;
            int min = packet.ReadInt32();
            int max = packet.ReadInt32();

            IList<ClientCard> cards = new List<ClientCard>();
            int count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int id = packet.ReadInt32();
                LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
                ClientCard card;
                if (((int)info.location & (int)CardLocation.Overlay) != 0)
                    card = new ClientCard(id, CardLocation.Overlay, -1, player);
                else if (info.location == 0)
                    card = new ClientCard(id, 0, 0, player);
                else
                    card = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
                if (card == null) continue;
                if (card.Id == 0)
                    card.SetId(id);
                cards.Add(card);
            }
            int count2 = packet.ReadInt32();
            for (int i = 0; i < count2; ++i)
            {
                int id = packet.ReadInt32();
                LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
            }

            IList<ClientCard> selected = func(cards, (finishable ? 0 : 1), 1, _select_hint, cancelable);

            if (selected.Count == 0 && cancelable)
            {
                Connection.Send(CtosMessage.Response, -1);
                return;
            }

            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);
            reply.Write(selected.Count);
            for (int i = 0; i < selected.Count; ++i)
            {
                int id = 0;
                for (int j = 0; j < count; ++j)
                {
                    if (cards[j] == null) continue;
                    if (cards[j].Equals(selected[i]))
                    {
                        id = j;
                        break;
                    }
                }
                reply.Write(id);
            }

            Connection.Send(reply);
        }

        private void OnSelectCard(BinaryReader packet)
        {
            InternalOnSelectCard(packet, _ai.OnSelectCard);
        }

        private void OnSelectUnselectCard(BinaryReader packet)
        {
            InternalOnSelectUnselectCard(packet, _ai.OnSelectCard);
        }

        private void OnSelectChain(BinaryReader packet)
        {
            packet.ReadByte(); // player
            packet.ReadByte(); // specount
            bool forced = packet.ReadByte() != 0;
            packet.ReadInt32(); // hint1
            packet.ReadInt32(); // hint2
            int count = packet.ReadInt32();

            IList<ClientCard> cards = new List<ClientCard>();
            IList<long> descs = new List<long>();

            for (int i = 0; i < count; ++i)
            {
                int id = packet.ReadInt32();
                LocationInfo info = new LocationInfo(packet, _duel.IsFirst);

                long desc = packet.ReadInt64();
                if (desc == 221) // trigger effect
                {
                    desc = 0;
                }

                ClientCard card = _duel.GetCard(info.controler, (int)info.location, info.sequence, info.position);
                if (card.Id == 0)
                    card.SetId(id);

                cards.Add(card);
                descs.Add(desc);
                packet.ReadByte(); // operation type
            }

            if (cards.Count == 0)
            {
                Connection.Send(CtosMessage.Response, -1);
                return;
            }

            if (cards.Count == 1 && forced)
            {
                Connection.Send(CtosMessage.Response, 0);
                return;
            }

            Connection.Send(CtosMessage.Response, _ai.OnSelectChain(cards, descs, forced));
        }

        private void OnSelectCounter(BinaryReader packet)
        {
            packet.ReadByte(); // player
            int type = packet.ReadInt16();
            short quantity = packet.ReadInt16();

            IList<ClientCard> cards = new List<ClientCard>();
            IList<int> counters = new List<int>();
            int count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                packet.ReadInt32(); // card id
                int player = GetLocalPlayer(packet.ReadByte());
                CardLocation loc = (CardLocation) packet.ReadByte();
                int seq = packet.ReadByte();
                cards.Add(_duel.GetCard(player, loc, seq));
                counters.Add(packet.ReadInt16());
            }

            IList<int> used = _ai.OnSelectCounter(type, quantity, cards, counters);
            byte[] result = new byte[used.Count * 2];
            for (int i = 0; i < used.Count; ++i)
            {
                result[i * 2] = (byte)(used[i] & 0xff);
                result[i * 2 + 1] = (byte)(used[i] >> 8);
            }
            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);
            reply.Write(result);
            Connection.Send(reply);
        }

        private void OnSelectDisfield(BinaryReader packet)
        {
            packet.ReadByte(); // player
            packet.ReadByte(); // TODO: min
            int field = ~packet.ReadInt32();

            int player;
            CardLocation location;
            int filter;
            if ((field & 0x7f0000) != 0)
            {
                player = 1;
                location = CardLocation.MonsterZone;
                filter = (field >> 16) & Zones.MonsterZones;
            }
            else if ((field & 0x1f000000) != 0)
            {
                player = 1;
                location = CardLocation.SpellZone;
                filter = (field >> 24) & Zones.SpellZones;
            }
            else if ((field & 0x7f) != 0)
            {
                player = 0;
                location = CardLocation.MonsterZone;
                filter = field & Zones.MonsterZones;
            }
            else if ((field & 0x1f00) != 0)
            {
                player = 0;
                location = CardLocation.SpellZone;
                filter = (field >> 8) & Zones.SpellZones;
            }
            else if ((field & 0x2000) != 0)
            {
                player = 0;
                location = CardLocation.FieldZone;
                filter = Zones.FieldZone;
            }
            else if ((field & 0xc000) != 0)
            {
                player = 0;
                location = CardLocation.PendulumZone;
                filter = (field >> 14) & Zones.PendulumZones;
            }
            else if ((field & 0x20000000) != 0)
            {
                player = 1;
                location = CardLocation.FieldZone;
                filter = Zones.FieldZone;
            }
            else
            {
                player = 1;
                location = CardLocation.PendulumZone;
                filter = (field >> 30) & Zones.PendulumZones;
            }

            int selected = _ai.OnSelectPlace(_select_hint, player, location, filter);
            _select_hint = 0;

            byte[] resp = new byte[3];
            resp[0] = (byte)GetLocalPlayer(player);

            if (location != CardLocation.PendulumZone && location != CardLocation.FieldZone)
            {
                resp[1] = (byte)location;
                if ((selected & filter) > 0)
                    filter &= selected;

                if ((filter & Zones.z2) != 0) resp[2] = 2;
                else if ((filter & Zones.z1) != 0) resp[2] = 1;
                else if ((filter & Zones.z3) != 0) resp[2] = 3;
                else if ((filter & Zones.z0) != 0) resp[2] = 0;
                else if ((filter & Zones.z4) != 0) resp[2] = 4;
                else if ((filter & Zones.z6) != 0) resp[2] = 6;
                else if ((filter & Zones.z5) != 0) resp[2] = 5;
            }
            else
            {
                resp[1] = (byte)CardLocation.SpellZone;
                if ((selected & filter) > 0)
                    filter &= selected;

                if ((filter & Zones.FieldZone) != 0) resp[2] = 5;
                if ((filter & Zones.z0) != 0) resp[2] = 6; // left pendulum zone
                if ((filter & Zones.z1) != 0) resp[2] = 7; // right pendulum zone
            }

            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);
            reply.Write(resp);
            Connection.Send(reply);
        }

        private void OnSelectEffectYn(BinaryReader packet)
        {
            packet.ReadByte(); // player

            int cardId = packet.ReadInt32();
            int player = GetLocalPlayer(packet.ReadByte());
            CardLocation loc = (CardLocation)packet.ReadByte();
            int seq = packet.ReadByte();
            packet.ReadByte();
            long desc = packet.ReadInt64();

            if (desc == 0 || desc == 221)
            {
                // 0: phase trigger effect
                // 221: trigger effect
                // for compatibility
                desc = -1;
            }

            ClientCard card = _duel.GetCard(player, loc, seq);
            if (card == null)
            {
                Connection.Send(CtosMessage.Response, 0);
                return;
            }

            if (card.Id == 0)
                card.SetId(cardId);

            int reply = _ai.OnSelectEffectYn(card, desc) ? (1) : (0);
            Connection.Send(CtosMessage.Response, (byte)reply);
        }

        private void OnSelectIdleCmd(BinaryReader packet)
        {
            packet.ReadByte(); // player

            _duel.MainPhase = new MainPhase();
            MainPhase main = _duel.MainPhase;
            int count;
            for (int k = 0; k < 5; k++)
            {
                count = packet.ReadInt32();
                for (int i = 0; i < count; ++i)
                {
                    packet.ReadInt32(); // card id
                    int con = GetLocalPlayer(packet.ReadByte());
                    CardLocation loc = (CardLocation)packet.ReadByte();
                    int seq = k == 2 ? packet.ReadByte() : packet.ReadInt32();
                    ClientCard card = _duel.GetCard(con, loc, seq);
                    if (card == null) continue;
                    card.ActionIndex[k] = i;
                    switch (k)
                    {
                        case 0:
                            main.SummonableCards.Add(card);
                            break;
                        case 1:
                            main.SpecialSummonableCards.Add(card);
                            break;
                        case 2:
                            main.ReposableCards.Add(card);
                            break;
                        case 3:
                            main.MonsterSetableCards.Add(card);
                            break;
                        case 4:
                            main.SpellSetableCards.Add(card);
                            break;
                    }
                }
            }
            count = packet.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                packet.ReadInt32(); // card id
                int con = GetLocalPlayer(packet.ReadByte());
                CardLocation loc = (CardLocation)packet.ReadByte();
                int seq = packet.ReadInt32();
                long desc = packet.ReadInt64();
                packet.ReadByte(); // operation type

                ClientCard card = _duel.GetCard(con, loc, seq);
                if (card == null) continue;
                card.ActionIndex[5] = i;
                if (card.ActionActivateIndex.ContainsKey(desc))
                    card.ActionActivateIndex.Remove(desc);
                card.ActionActivateIndex.Add(desc, i);
                main.ActivableCards.Add(card);
                main.ActivableDescs.Add(desc);
            }

            main.CanBattlePhase = packet.ReadByte() != 0;
            main.CanEndPhase = packet.ReadByte() != 0;
            packet.ReadByte(); // CanShuffle

            Connection.Send(CtosMessage.Response, _ai.OnSelectIdleCmd(main).ToValue());
        }

        private void OnSelectOption(BinaryReader packet)
        {
            IList<long> options = new List<long>();
            packet.ReadByte(); // player
            int count = packet.ReadByte();
            for (int i = 0; i < count; ++i)
                options.Add(packet.ReadInt64());
            Connection.Send(CtosMessage.Response, _ai.OnSelectOption(options));
        }

        private void OnSelectPlace(BinaryReader packet)
        {
            packet.ReadByte(); // player
            packet.ReadByte(); // min
            int field = ~packet.ReadInt32();

            int player;
            CardLocation location;
            int filter;

            if ((field & 0x7f) != 0)
            {
                player = 0;
                location = CardLocation.MonsterZone;
                filter = field & Zones.MonsterZones;
            }
            else if ((field & 0x1f00) != 0)
            {
                player = 0;
                location = CardLocation.SpellZone;
                filter = (field >> 8) & Zones.SpellZones;
            }
            else if ((field & 0x2000) != 0)
            {
                player = 0;
                location = CardLocation.FieldZone;
                filter = Zones.FieldZone;
            }
            else if ((field & 0xc000) != 0)
            {
                player = 0;
                location = CardLocation.PendulumZone;
                filter = (field >> 14) & Zones.PendulumZones;
            }
            else if ((field & 0x7f0000) != 0)
            {
                player = 1;
                location = CardLocation.MonsterZone;
                filter = (field >> 16) & Zones.MonsterZones;
            }
            else if ((field & 0x1f000000) != 0)
            {
                player = 1;
                location = CardLocation.SpellZone;
                filter = (field >> 24) & Zones.SpellZones;
            }
            else if ((field & 0x20000000) != 0)
            {
                player = 1;
                location = CardLocation.FieldZone;
                filter = Zones.FieldZone;
            }
            else
            {
                player = 1;
                location = CardLocation.PendulumZone;
                filter = (field >> 30) & Zones.PendulumZones;
            }

            int selected = _ai.OnSelectPlace(_select_hint, player, location, filter);
            _select_hint = 0;

            byte[] resp = new byte[3];
            resp[0] = (byte)GetLocalPlayer(player);

            if (location != CardLocation.PendulumZone && location != CardLocation.FieldZone)
            {
                resp[1] = (byte)location;
                if ((selected & filter) > 0)
                    filter &= selected;

                if ((filter & Zones.z2) != 0) resp[2] = 2;
                else if ((filter & Zones.z1) != 0) resp[2] = 1;
                else if ((filter & Zones.z3) != 0) resp[2] = 3;
                else if ((filter & Zones.z0) != 0) resp[2] = 0;
                else if ((filter & Zones.z4) != 0) resp[2] = 4;
                else if ((filter & Zones.z6) != 0) resp[2] = 6;
                else if ((filter & Zones.z5) != 0) resp[2] = 5;
            }
            else
            {
                resp[1] = (byte)CardLocation.SpellZone;
                if ((selected & filter) > 0)
                    filter &= selected;

                if ((filter & Zones.FieldZone) != 0) resp[2] = 5;
                if ((filter & Zones.z0) != 0) resp[2] = 6; // left pendulum zone
                if ((filter & Zones.z1) != 0) resp[2] = 7; // right pendulum zone
            }

            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);
            reply.Write(resp);
            Connection.Send(reply);
        }

        private void OnSelectPosition(BinaryReader packet)
        {
            packet.ReadByte(); // player
            int cardId = packet.ReadInt32();
            int pos = packet.ReadByte();
            if (pos == 0x1 || pos == 0x2 || pos == 0x4 || pos == 0x8)
            {
                Connection.Send(CtosMessage.Response, pos);
                return;
            }
            IList<CardPosition> positions = new List<CardPosition>();
            if ((pos & (int)CardPosition.FaceUpAttack) != 0)
                positions.Add(CardPosition.FaceUpAttack);
            if ((pos & (int)CardPosition.FaceDownAttack) != 0)
                positions.Add(CardPosition.FaceDownAttack);
            if ((pos & (int)CardPosition.FaceUpDefence) != 0)
                positions.Add(CardPosition.FaceUpDefence);
            if ((pos & (int)CardPosition.FaceDownDefence) != 0)
                positions.Add(CardPosition.FaceDownDefence);
            Connection.Send(CtosMessage.Response, (int)_ai.OnSelectPosition(cardId, positions));
        }

        private void OnSelectSum(BinaryReader packet)
        {
            packet.ReadByte(); // player
            bool mode = packet.ReadByte() == 0;
            int sumval = packet.ReadInt32();
            int min = packet.ReadInt32();
            int max = packet.ReadInt32();

            if (max <= 0)
                max = 99;

            IList<ClientCard> mandatoryCards = new List<ClientCard>();
            IList<ClientCard> cards = new List<ClientCard>();

            for (int j = 0; j < 2; ++j)
            {
                int count = packet.ReadInt32();
                for (int i = 0; i < count; ++i)
                {
                    int cardId = packet.ReadInt32();
                    LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
                    ClientCard card = _duel.GetCard(info.controler, (int)info.location, info.sequence, info.position);
                    if (cardId != 0 && card.Id != cardId)
                        card.SetId(cardId);
                    card.SelectSeq = i;
                    int OpParam = packet.ReadInt32();
                    int OpParam1 = OpParam & 0xffff;
                    int OpParam2 = OpParam >> 16;
                    if (OpParam2 > 0 && OpParam1 > OpParam2)
                    {
                        card.OpParam1 = OpParam2;
                        card.OpParam2 = OpParam1;
                    }
                    else
                    {
                        card.OpParam1 = OpParam1;
                        card.OpParam2 = OpParam2;
                    }
                    if (j == 0)
                        mandatoryCards.Add(card);
                    else
                        cards.Add(card);
                }
            }

            for (int k = 0; k < mandatoryCards.Count; ++k)
            {
                sumval -= mandatoryCards[k].OpParam1;
            }

            IList<ClientCard> selected = _ai.OnSelectSum(cards, sumval, min, max, _select_hint, mode);
            _select_hint = 0;
            byte[] result = new byte[mandatoryCards.Count + selected.Count + 16];
            int selection_value = 2;
            result[0] = (byte)(selection_value & 0xff);
            result[1] = (byte)((selection_value >> 4) & 0xff);
            result[2] = (byte)((selection_value >> 8) & 0xff);
            result[3] = (byte)((selection_value >> 16) & 0xff);


            uint tot_count = (uint)(mandatoryCards.Count + selected.Count);

            result[4] = (byte)(tot_count & 0xff);
            result[5] = (byte)((tot_count >> 4) & 0xff);
            result[6] = (byte)((tot_count >> 8) & 0xff);
            result[7] = (byte)((tot_count >> 16) & 0xff);

            int index = 8;

            while (index <= mandatoryCards.Count)
            {
                result[index++] = 0;
            }
            int l = 0;
            while (l < selected.Count)
            {
                result[index++] = (byte)selected[l].SelectSeq;
                ++l;
            }

            BinaryWriter reply = GamePacketFactory.Create(CtosMessage.Response);
            reply.Write(result);
            Connection.Send(reply);
        }

        private void OnSelectTribute(BinaryReader packet)
        {
            InternalOnSelectCard(packet, _ai.OnSelectTribute, true);
        }

        private void OnSelectYesNo(BinaryReader packet)
        {
            packet.ReadByte(); // player
            long desc = packet.ReadInt64();
            int reply;
            if (desc == 30)
                reply = _ai.OnSelectBattleReplay() ? 1 : 0;
            else if (desc == 1989)
                reply = 1;
            else
                reply = _ai.OnSelectYesNo(desc) ? 1 : 0;
            Connection.Send(CtosMessage.Response, (byte)reply);
        }

        private void OnAnnounceAttrib(BinaryReader packet)
        {
            IList<CardAttribute> attributes = new List<CardAttribute>();
            packet.ReadByte(); // player
            int count = packet.ReadByte();
            int available = packet.ReadInt32();
            int filter = 0x1;
            for (int i = 0; i < 7; ++i)
            {
                if ((available & filter) != 0)
                    attributes.Add((CardAttribute) filter);
                filter <<= 1;
            }
            attributes = _ai.OnAnnounceAttrib(count, attributes);
            int reply = 0;
            for (int i = 0; i < count; ++i)
                reply += (int)attributes[i];
            Connection.Send(CtosMessage.Response, (byte)reply);
        }

        private void OnAnnounceCard(BinaryReader packet)
        {
            IList<long> opcodes = new List<long>();
            packet.ReadByte(); // player
            int count = packet.ReadByte();
            bool token = false;
            bool alias = false;
            for (int i = 0; i < count; ++i)
            {
                long opcode = packet.ReadInt64();
                if (opcode == Opcodes.OPCODE_ALLOW_ALIASES)
                    alias = true;
                else if (opcode == Opcodes.OPCODE_ALLOW_TOKENS)
                    token = true;
                else
                    opcodes.Add(opcode);
            }

            IList<int> avail = new List<int>();
            IList<NamedCard> all = NamedCardsManager.GetAllCards();
            foreach (NamedCard card in all)
            {
                if (card.HasType(CardType.Token) && !token)
                    continue;
                if (card.Alias > 0 && !alias)
                    continue;
                LinkedList<long> stack = new LinkedList<long>();
                for (int i = 0; i < opcodes.Count; i++)
                {
                    switch (opcodes[i])
                    {
                        case Opcodes.OPCODE_ADD:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(lhs + rhs);
                            }
                            break;
                        case Opcodes.OPCODE_SUB:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(lhs - rhs);
                            }
                            break;
                        case Opcodes.OPCODE_MUL:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(lhs * rhs);
                            }
                            break;
                        case Opcodes.OPCODE_DIV:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(lhs / rhs);
                            }
                            break;
                        case Opcodes.OPCODE_AND:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(((rhs & lhs) != 0) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_OR:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(((rhs | lhs) != 0) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_NEG:
                            if (stack.Count >= 1)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(-rhs);
                            }
                            break;
                        case Opcodes.OPCODE_NOT:
                            if (stack.Count >= 1)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast((rhs != 0) ? 0 : 1);
                            }
                            break;
                        case Opcodes.OPCODE_BAND:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(rhs & lhs);
                            }
                            break;
                        case Opcodes.OPCODE_BOR:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(rhs | lhs);
                            }
                            break;
                        case Opcodes.OPCODE_BNOT:
                            if (stack.Count >= 1)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(~rhs);
                            }
                            break;
                        case Opcodes.OPCODE_BXOR:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(rhs ^ lhs);
                            }
                            break;
                        case Opcodes.OPCODE_LSHIFT:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(rhs << (int)lhs);
                            }
                            break;
                        case Opcodes.OPCODE_RSHIFT:
                            if (stack.Count >= 2)
                            {
                                long rhs = stack.Last.Value;
                                stack.RemoveLast();
                                long lhs = stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(rhs >> (int)lhs);
                            }
                            break;
                        case Opcodes.OPCODE_ISCODE:
                            if (stack.Count >= 1)
                            {
                                uint code = (uint)stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast((code == card.Id) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_ISSETCARD:
                            if (stack.Count >= 1)
                            {
                                int set = (int)stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast((card.HasSetcode(set)) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_ISTYPE:
                            if (stack.Count >= 1)
                            {
                                int type = (int)stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(((type & card.Type) != 0) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_ISRACE:
                            if (stack.Count >= 1)
                            {
                                ulong race = (ulong)stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(((race & card.Race) != 0) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_ISATTRIBUTE:
                            if (stack.Count >= 1)
                            {
                                int attr = (int)stack.Last.Value;
                                stack.RemoveLast();
                                stack.AddLast(((attr & card.Attribute) != 0) ? 1 : 0);
                            }
                            break;
                        case Opcodes.OPCODE_GETCODE:
                            if (stack.Count >= 1)
                            {
                                stack.AddLast(card.Id);
                            }
                            break;
                        case Opcodes.OPCODE_GETTYPE:
                            if (stack.Count >= 1)
                            {
                                stack.AddLast(card.Type);
                            }
                            break;
                        case Opcodes.OPCODE_GETRACE:
                            if (stack.Count >= 1)
                            {
                                stack.AddLast((long)card.Race);
                            }
                            break;
                        case Opcodes.OPCODE_GETATTRIBUTE:
                            if (stack.Count >= 1)
                            {
                                stack.AddLast(card.Attribute);
                            }
                            break;
                        default:
                            stack.AddLast(opcodes[i]);
                            break;
                    }
                }
                if (stack.Count == 1) {
                    long val = stack.Last.Value;
                    stack.RemoveLast();
                    if (val != 0)
                        avail.Add(card.Id);
                }
            }
            if (avail.Count == 0)
                throw new Exception("No avail card found for announce!");
            Connection.Send(CtosMessage.Response, _ai.OnAnnounceCard(avail));
        }

        private void OnAnnounceNumber(BinaryReader packet)
        {
            IList<int> numbers = new List<int>();
            packet.ReadByte(); // player
            int count = packet.ReadByte();
            for (int i = 0; i < count; ++i)
                numbers.Add((int)packet.ReadInt64());
            Connection.Send(CtosMessage.Response, _ai.OnAnnounceNumber(numbers));
        }

        private void OnAnnounceRace(BinaryReader packet)
        {
            IList<CardRace> races = new List<CardRace>();
            packet.ReadByte(); // player
            int count = packet.ReadByte();
            ulong available = packet.ReadUInt64();
            ulong filter = 0x1;
            for (int i = 0; i < 30; ++i)
            {
                if ((available & filter) != 0)
                    races.Add((CardRace)filter);
                filter <<= 1;
            }
            races = _ai.OnAnnounceRace(count, races);
            ulong reply = 0;
            for (int i = 0; i < count; ++i)
                reply |= (ulong)races[i];
            Connection.Send(CtosMessage.Response, (long)reply);
        }

        private void OnRockPaperScissors(BinaryReader packet)
        {
            packet.ReadByte(); // player
            int result;
            if (_hand > 0)
                result = _hand;
            else
                result = _ai.OnRockPaperScissors();
            Connection.Send(CtosMessage.Response, (byte)result);
        }

        private void OnEquip(BinaryReader packet)
        {
            LocationInfo info1 = new LocationInfo(packet, _duel.IsFirst);
            LocationInfo info2 = new LocationInfo(packet, _duel.IsFirst);
            ClientCard equipCard = _duel.GetCard(info1.controler, (CardLocation)info1.location, info1.sequence);
            ClientCard targetCard = _duel.GetCard(info2.controler, (CardLocation)info2.location, info2.sequence);
            if (equipCard == null || targetCard == null) return;
            if (equipCard.EquipTarget != null)
            {
                equipCard.EquipTarget.EquipCards.Remove(equipCard);
            }
            equipCard.EquipTarget = targetCard;
            targetCard.EquipCards.Add(equipCard);
        }

        private void OnUnEquip(BinaryReader packet)
        {
            LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
            ClientCard equipCard = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
            if (equipCard == null) return;
            if (equipCard.EquipTarget != null)
            {
                equipCard.EquipTarget.EquipCards.Remove(equipCard);
                equipCard.EquipTarget = null;
            }
        }

        private void OnCardTarget(BinaryReader packet)
        {
            LocationInfo info1 = new LocationInfo(packet, _duel.IsFirst);
            LocationInfo info2 = new LocationInfo(packet, _duel.IsFirst);
            ClientCard ownerCard = _duel.GetCard(info1.controler, (CardLocation)info1.location, info1.sequence);
            ClientCard targetCard = _duel.GetCard(info2.controler, (CardLocation)info2.location, info2.sequence);
            if (ownerCard == null || targetCard == null) return;
            ownerCard.TargetCards.Add(targetCard);
            targetCard.OwnTargets.Add(ownerCard);
        }

        private void OnCancelTarget(BinaryReader packet)
        {
            LocationInfo info1 = new LocationInfo(packet, _duel.IsFirst);
            LocationInfo info2 = new LocationInfo(packet, _duel.IsFirst);
            ClientCard ownerCard = _duel.GetCard(info1.controler, (CardLocation)info1.location, info1.sequence);
            ClientCard targetCard = _duel.GetCard(info2.controler, (CardLocation)info2.location, info2.sequence);
            if (ownerCard == null || targetCard == null) return;
            ownerCard.TargetCards.Remove(targetCard);
            targetCard.OwnTargets.Remove(ownerCard);
        }

        private void OnSummoning(BinaryReader packet)
        {
            _duel.LastSummonedCards.Clear();
            int code = packet.ReadInt32();
            LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
            ClientCard card = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
            _duel.SummoningCards.Add(card);
            _duel.LastSummonPlayer = info.controler;
        }

        private void OnSummoned(BinaryReader packet)
        {
            foreach (ClientCard card in _duel.SummoningCards)
            {
                _duel.LastSummonedCards.Add(card);
            }
            _duel.SummoningCards.Clear();
        }

        private void OnSpSummoning(BinaryReader packet)
        {
            _duel.LastSummonedCards.Clear();
            _ai.CleanSelectMaterials();
            int code = packet.ReadInt32();
            LocationInfo info = new LocationInfo(packet, _duel.IsFirst);
            ClientCard card = _duel.GetCard(info.controler, (CardLocation)info.location, info.sequence);
            _duel.SummoningCards.Add(card);
            _duel.LastSummonPlayer = info.controler;
        }

        private void OnSpSummoned(BinaryReader packet)
        {
            foreach (ClientCard card in _duel.SummoningCards)
            {
                card.IsSpecialSummoned = true;
                _duel.LastSummonedCards.Add(card);
            }
            _duel.SummoningCards.Clear();
        }
    }
}

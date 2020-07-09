using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Concurrent;
using System.Collections;

namespace paxos
{
    public class Logger
    {
        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        private static bool isInit = false;
        private static readonly object loglock = new object();

        public static void log(Object obj)
        {
            lock (loglock)
            {
                if (!isInit)
                {
                    isInit = true;
                    AllocConsole();
                    Console.WriteLine("open console successfully");
                }
                string res = obj.ToString();
                Console.WriteLine(res);
            }
        }

        public static void debug(Object obj)
        {
            if (Util.debug)
            {
                log(obj);
            }
        }

        public static void error(Object obj)
        {
            log(obj);
        }
    }

    class Render
    {
        static private System.Windows.Shapes.Path getArrow(Point p1, Point p2, byte[] color, int thinckness)
        {
            if (thinckness <= 0)
            {
                return null;
            }
            GeometryGroup lineGroup = new GeometryGroup();
            double theta = Math.Atan2((p2.Y - p1.Y), (p2.X - p1.X)) * 180 / Math.PI;

            PathGeometry pathGeometry = new PathGeometry();
            PathFigure pathFigure = new PathFigure();
            Point p = new Point(p1.X + ((p2.X - p1.X) / 1.35), p1.Y + ((p2.Y - p1.Y) / 1.35));
            pathFigure.StartPoint = p;

            Point lpoint = new Point(p.X + 6, p.Y + 15);
            Point rpoint = new Point(p.X - 6, p.Y + 15);
            LineSegment seg1 = new LineSegment();
            seg1.Point = lpoint;
            pathFigure.Segments.Add(seg1);

            LineSegment seg2 = new LineSegment();
            seg2.Point = rpoint;
            pathFigure.Segments.Add(seg2);

            LineSegment seg3 = new LineSegment();
            seg3.Point = p;
            pathFigure.Segments.Add(seg3);

            pathGeometry.Figures.Add(pathFigure);
            RotateTransform transform = new RotateTransform();
            transform.Angle = theta + 90;
            transform.CenterX = p.X;
            transform.CenterY = p.Y;
            pathGeometry.Transform = transform;
            lineGroup.Children.Add(pathGeometry);

            LineGeometry connectorGeometry = new LineGeometry();
            connectorGeometry.StartPoint = p1;
            connectorGeometry.EndPoint = p2;
            lineGroup.Children.Add(connectorGeometry);
            System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
            path.Data = lineGroup;
            path.StrokeThickness = thinckness * 2;
            path.Stroke = path.Fill = new SolidColorBrush(Color.FromRgb(color[0], color[1], color[2]));
            return (path);
        }

        public static void render(UIElementCollection uis, Canvas canvas)
        {
            List<UIElement> filter = new List<UIElement>();
            foreach (UIElement it in uis)
            {
                if (it is System.Windows.Shapes.Path)
                {
                    filter.Add(it);
                }
            }
            foreach (UIElement it in filter)
            {
                uis.Remove(it);
            }
            List<System.Windows.Shapes.Path> paths = new List<System.Windows.Shapes.Path>();
            int curOdd = 1;
            foreach (UIElement it in uis)
            {
                if (it is Ellipse)
                {
                    var cur = (it as Ellipse);
                    if (cur.Name == "Example1" || cur.Name == "Example2")
                    {
                        continue;
                    }
                    curOdd = 0 - curOdd;
                    int curOffset = curOdd * 10;
                    var rm = (RM)cur.Tag;
                    cur.Stroke = new SolidColorBrush(Color.FromRgb(rm.rgb[0], rm.rgb[1], rm.rgb[2])); ;
                    cur.SetValue(Canvas.LeftProperty, (double)rm.x);
                    cur.SetValue(Canvas.TopProperty, (double)rm.y);
                    foreach (var i in rm.connectedRms)
                    {
                        var arrow = getArrow(new Point(i.Key.x + curOffset, i.Key.y + curOffset),
                            new Point(rm.x + curOffset, rm.y + curOffset), new byte[] { 0, 0, 100 },
                             Paxos.arrowMaxThiness() - (int)(Paxos.getTime() - (int)i.Value) / Paxos.arrowThinessTimeSlide());
                        if (arrow != null)
                        {
                            paths.Add(arrow);
                        }
                    }
                }
                else if (it is Rectangle)
                {

                    var cur = (it as Rectangle);
                    if (cur.Name == "Example1" || cur.Name == "Example2")
                    {
                        continue;
                    }
                    curOdd = 0 - curOdd;
                    int curOffset = curOdd * 10;
                    var rm = (RM)cur.Tag;
                    cur.Stroke = new SolidColorBrush(Color.FromRgb(rm.rgb[0], rm.rgb[1], rm.rgb[2])); ;
                    cur.SetValue(Canvas.LeftProperty, (double)rm.x);
                    cur.SetValue(Canvas.TopProperty, (double)rm.y);
                    foreach (var i in rm.connectedRms)
                    {
                        var arrow = getArrow(new Point(i.Key.x + curOffset, i.Key.y + curOffset),
                            new Point(rm.x + curOffset, rm.y + curOffset), new byte[] { 100, 0, 0 },
                            Paxos.arrowMaxThiness() - (int)(Paxos.getTime() - (int)i.Value) / Paxos.arrowThinessTimeSlide());
                        if (arrow != null)
                        {
                            paths.Add(arrow);
                        }
                    }
                }
            }
            foreach (System.Windows.Shapes.Path it in paths)
            {
                canvas.Children.Add(it);
            }
        }
    }

    [Serializable]
    public class Msg
    {
        public int era;
        public int msgId;
        public typeEnum type;
        public int data;
        public int max1a;//receive max 1a;
        public int max2b;//send max 2b;
        public int sourcePort;

        public enum typeEnum
        {
            step1a,
            step1bFree,
            step1bForced,
            step2a,
            step2b,
            step3,
            eraHasValue,
            getEraValue
        }


        public Msg(int sourcePort, int era, typeEnum type, int msgId, int data)
        {
            this.sourcePort = sourcePort;
            this.msgId = msgId;
            this.data = data;
            this.era = era;
            this.type = type;
        }

        override
        public string ToString()
        {
            return string.Format("Msg: {0} {1} {2} {3}", era, type.ToString(), msgId, data);
        }
    }

    public class MsgPacket : IComparable
    {

        private long uid;
        public long time;
        public int targetPort;
        public Msg msg;
        public MsgPacket(Msg msg, long time, bool depreciated, int targetPort)
        {
            lock (seedlock)
            {
                this.msg = msg;
                this.time = time;
                this.targetPort = targetPort;
                uid = seed++;
            }
        }

        private static object seedlock = new object();
        private static long seed = 0;

        public int CompareTo(object y)
        {
            if (y is MsgPacket)
            {
                MsgPacket cur = (y as MsgPacket);
                return time == cur.time ?
                    (uid < cur.uid ? -1 : 1) : (time < cur.time ? -1 : 1);
            }
            return 1;
        }
    }

    public class Util
    {
        public static Random rnd = new Random(111);

        public static bool debug = false;

        public static long randSendDelayTime()
        {
            if (debug)
            {
                return 0;
            }
            return rnd.Next(1, 4);
        }

        public static bool randCorrupt(RM rm)
        {
            if (debug)
            {
                return false;
            }
            return rnd.Next(0, 1000) >= 990;
        }

        public static long randCorruptTime()
        {
            if (debug)
            {
                return 0;
            }
            return rnd.Next(2, 11);
        }

        public static bool randDepreciate()
        {
            if (debug)
            {
                return false;
            }
            return rnd.Next(0, 100) > 80;
        }

        public static int randRgb()
        {
            return rnd.Next(0, 255) * 256 * 256 + rnd.Next(0, 255) * 256 + rnd.Next(0, 255);
        }

        public static bool displayUI()
        {
            return true;
        }

        public static bool randPropose(RM rm)
        {
            if (debug)
            {
                return true;
            }
            return rnd.Next(0, 100) > 88;
        }

        public static T desearial<T>(byte[] data)
        {
            byte[] bytes = data;
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream(bytes);
            T obj = (T)formatter.Deserialize(stream);
            stream.Close();
            return obj;
        }

        public static byte[] searial(Object obj)
        {
            byte[] bytes;
            IFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, obj);
                bytes = stream.ToArray();
            }
            return bytes;
        }

    }

    public class Connector
    {
        static private ConcurrentPriorityQueue<long, MsgPacket> msgs = new ConcurrentPriorityQueue<long, MsgPacket>();

        public bool send(Msg msg, int port)
        {
            if (!Util.randDepreciate())
            {
                MsgPacket packet = new MsgPacket(msg, Paxos.getTime() + Util.randSendDelayTime(), false, port);
                var item = new KeyValuePair<long, MsgPacket>(packet.time, packet);
                msgs.Enqueue(item);
                return true;
            }else
            {
                return false;
            }
        }

        static bool isStartConsume = false;
        public static void startConsumeThread()
        {
            if (isStartConsume)
            {
                return;
            }
            isStartConsume = true;
            ThreadStart job = new ThreadStart(consume);
            Thread thread = new Thread(job);
            thread.Start();
        }

        public static void startConsumeThread(int nThead)
        {
            if (isStartConsume)
            {
                return;
            }
            isStartConsume = true;
            for(int i = 0; i < nThead; i++)
            {
                ThreadStart job = new ThreadStart(consume);
                Thread thread = new Thread(job);
                thread.Start();
            }
        }

        public static bool consumeCurrent()
        {
            bool isConsumeSomeTask = false;
            for (;;)
            {
                KeyValuePair<long, MsgPacket> res = new KeyValuePair<long, MsgPacket>();
                if (msgs.TryDequeue(out res))
                {
                    MsgPacket msg = res.Value;
                    if (msg.time > Paxos.getTime())
                    {
                        msgs.Enqueue(res);
                        return isConsumeSomeTask;
                    }
                    Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    IPAddress broadcast = IPAddress.Parse("127.0.0.1");
                    IPEndPoint ep = new IPEndPoint(broadcast, msg.targetPort);
                    s.SendTo(Util.searial(msg.msg), ep);
                    s.Close();
                    isConsumeSomeTask = true;
                }else
                {
                    return isConsumeSomeTask;
                }
            }
        }

        private static void consume()
        {
            for (;;)
            {
                KeyValuePair<long, MsgPacket> res = new KeyValuePair<long, MsgPacket>();
                if(msgs.TryDequeue(out res))
                {
                    MsgPacket msg = res.Value;
                    if (msg.time > Paxos.getTime())
                    {
                        msgs.Enqueue(res);
                        continue;
                    }
                    Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    IPAddress broadcast = IPAddress.Parse("127.0.0.1");
                    IPEndPoint ep = new IPEndPoint(broadcast, msg.targetPort);
                    s.SendTo(Util.searial(msg.msg), ep);
                }

            }
        }

        private IPEndPoint groupEP;
        private UdpClient listener;
        public int localPort;
        public Connector()
        {
            listener = new UdpClient(0);
            localPort = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
            groupEP = new IPEndPoint(IPAddress.Any, localPort);
        }

        public List<Msg> getMsgs()
        {
            List<Msg> msgs = new List<Msg>();
            while (listener.Available > 0)
            {
                msgs.Add(Util.desearial<Msg>(listener.Receive(ref groupEP)));
            }
            return msgs;
        }
    }


    public class RM
    {
        public int id;
        //use to draw ui
        public int x, y;
        public Paxos parent;
        public int curEra = 0;
        //rm's color in the ui
        public byte[] rgb = new byte[3];
        //store the rm communicated mostly recently time,value is use to demonstrate the gap time
        public Dictionary<RM, long> connectedRms = new Dictionary<RM, long>();
        public Connector connector = new Connector();
        public List<Msg> receiveMsgHistory = new List<Msg>();
        public List<Msg> senedMsgHistory = new List<Msg>();
        public State state;
        public List<Msg> preReceiveMsgForce;
        public List<Msg> preReceiveMsgFree;

        public void send(Msg msg, int port)
        {
            if (Util.debug)
            {
                RM source = parent.portToRM(msg.sourcePort);
                RM target = parent.portToRM(port);
                if (source is Acceptor && target is Acceptor)
                {
                    Logger.log("source and target both are acceptor");
                }
                else if (source.id == target.id)
                {
                    Logger.log("source and target are the same");
                }
            }
            if(connector.send(msg, port))
            {
                senedMsgHistory.Insert(0, msg);
            }
        }

        public virtual void act()
        {
            List<Msg> msgs = connector.getMsgs();
            foreach (Msg msg in msgs)
            {
                connectedRms[parent.portToRM(msg.sourcePort)] = Paxos.getTime();
                receiveMsgHistory.Insert(0, msg);
                state = state.execute(msg);
            }
            state = state.execute(null);
        }
    }


    public class Acceptor : RM
    {
        public Acceptor()
        {
            state = new AcceptorState(this);
        }
    }

    public class Proposer : RM
    {
        public int proposeValue = -1;
        public Proposer()
        {
            state = new ProposerWorkState(this, -1);
        }
    }


    public class State
    {
        public RM rm;
        public int sendedMsgId;
        public long createTime;

        public State(RM rm,int sendedMsgId)
        {
            this.sendedMsgId = sendedMsgId;
            createTime = Paxos.getTime();
            this.rm = rm;
        }

        public State getUnreasonableMsg(Msg msg)
        {
            Logger.debug("unreasonable message");
            return this;
        }

        public virtual State execute(Msg msg)
        {
            return this;
        }

        public void willnotReplyMsg(Msg msg)
        {
            Logger.debug("do not reply id:" + (rm.parent.portToRM(msg.sourcePort)).id + " with " + msg);
        }

        protected virtual bool processDiffEraMsg(Msg msg)
        {
            if (rm.curEra != msg.era)
            {
                if (rm.curEra < msg.era)
                {
                    Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.getEraValue, sendedMsgId, -1);
                    rm.send(sendMsg, msg.sourcePort);
                }
                else if (rm.curEra > msg.era)
                {
                    //prevent infinite loop
                    if (msg.type == Msg.typeEnum.eraHasValue)
                    {
                        return true;
                    }
                    Msg sendMsg = new Msg(rm.connector.localPort, msg.era, Msg.typeEnum.eraHasValue, sendedMsgId, rm.parent.getEraValue(msg.era));
                    rm.send(sendMsg, msg.sourcePort);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public State getEraValue(Msg msg)
        {
            if (msg.era >= rm.curEra)
            {
                Logger.log("error rm " + rm.ToString() + " didn't has ear data:" + msg.ToString());
            }
            else
            {
                Msg sendMsg = new Msg(rm.connector.localPort, msg.era, Msg.typeEnum.eraHasValue, sendedMsgId, rm.parent.getEraValue(msg.era));
                rm.send(sendMsg, msg.sourcePort);
            }
            return this;
        }

        protected int getDistinctSource(List<Msg> msg)
        {
            SortedSet<int> filter = new SortedSet<int>();
            foreach (Msg it in msg)
            {
                filter.Add(it.sourcePort);
            }
            return filter.Count;
        }

        protected int getDistinctSource(List<Msg> msg,List<Msg> msg2)
        {
            SortedSet<int> filter = new SortedSet<int>();
            foreach (Msg it in msg)
            {
                filter.Add(it.sourcePort);
            }
            foreach (Msg it in msg2)
            {
                filter.Add(it.sourcePort);
            }
            return filter.Count;
        }

        protected State debugProcessRemainMsg(Msg msg)
        {
            RM sourceRm = rm.parent.portToRM(msg.sourcePort);
            Logger.debug("some error happen," + rm.ToString() + " can't process msg:" + msg.ToString());
            Logger.debug("source rm is: " + sourceRm.ToString());
            return this;
        }
    }

    public class CorruptState : State
    {
        private State revertState;
        private long reverseTime;
        public override State execute(Msg msg)
        {
            if (reverseTime <= Paxos.getTime())
            {
                return revertState;
            }
            return this;
        }
        public CorruptState(RM rm, State storedState, long reverseTime) : base(rm,-1)
        {
            Logger.debug(rm.GetType().Name + " " + rm.id + " corrupt");
            revertState = storedState;
            this.reverseTime = reverseTime;
        }
    }

    public class ProposerState : State
    {
        public ProposerState(RM rm, int sendedMsgId) : base(rm,sendedMsgId)
        {
        }

        public State eraHasValue(Msg msg)
        {
            if (msg.era < rm.curEra)
            {
                return this;
            }
            rm.curEra++;
            (rm as Proposer).proposeValue = -1;
            return new ProposerWorkState(rm, -1);

        }
        //我猜，一个过时的消息是毫无用处的，首先你基于小的msgid的消息处理出结果，就只能再发出小的msgid消息，但现在你已经发过大的msgid
        //就很有可能会被忽略。
        public bool processDiffMsgId(Msg msg)
        {
            //sometime ，other proposer will send step3 message to it
            if (msg.type == Msg.typeEnum.step3|| msg.type==Msg.typeEnum.eraHasValue)
            {
                return false;
            }
            if (msg.msgId > sendedMsgId)
            {
                Logger.error("error,reviced bigger msg id");
                return true;
            }else if(msg.msgId <sendedMsgId)
            {
                Logger.debug("get pre msgId");
                return true;
            }
            return false;
        }
    }

    public class ProposerWorkState : ProposerState
    {
        public ProposerWorkState(RM rm, int sendedMsgId) : base(rm, sendedMsgId)
        {
        }

        private State tryStart1a()
        {
            if (Util.randPropose(rm) == false)
            {
                return this;
            }
            int nextMsgId = (sendedMsgId / 10 + 1) * 10 + rm.id;
            Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.step1a, nextMsgId, -1);
            List<Acceptor> acceptors = rm.parent.getAllAcceptor();
            foreach (Acceptor i in acceptors)
            {
                rm.send(sendMsg, i.connector.localPort);
            }
            ProposerWait1bState res = new ProposerWait1bState(rm, nextMsgId);
            return res;
        }

        public override State execute(Msg msg)
        {
            if (msg == null)
            {
                //simulate corrupt
                if (Util.randCorrupt(rm))
                {
                    CorruptState state = new CorruptState(rm, this, Util.randCorruptTime() + Paxos.getTime());
                    return state;
                }
                if (createTime + rm.parent.getWaitWorkMaxTime() < Paxos.getTime())
                {
                    return new ProposerWorkState(rm, sendedMsgId);
                }
                return tryStart1a();
            }
            else
            {
                if (processDiffEraMsg(msg) || processDiffMsgId(msg))
                {
                    return this;
                }

                State res = this;
                switch (msg.type)
                {
                    case Msg.typeEnum.step1bFree:
                    case Msg.typeEnum.step1bForced:
                    case Msg.typeEnum.getEraValue:
                        res = getUnreasonableMsg(msg);
                        break;
                    case Msg.typeEnum.eraHasValue:
                    case Msg.typeEnum.step3:
                        res = eraHasValue(msg);
                        break;
                    default:
                        res = debugProcessRemainMsg(msg);
                        break;
                }
                return res;
            }
        }
    }

    public class ProposerWait1bState : ProposerState
    {
        List<Msg> freemsg = new List<Msg>();
        List<Msg> forcedmsg = new List<Msg>();
        public ProposerWait1bState(RM rm, int sendedMsgId) : base(rm, sendedMsgId)
        {
        }

        private State step1b(Msg msg)
        {
            if (msg.type == Msg.typeEnum.step1bForced)
            {
                forcedmsg.Add(msg);
            }
            else if (msg.type == Msg.typeEnum.step1bFree)
            {
                freemsg.Add(msg);
            }else
            {
                Logger.log("error no step1b message");
            }

            if(getDistinctSource(freemsg,forcedmsg)> rm.parent.getAllAcceptor().Count / 2)
            {
                rm.preReceiveMsgForce = forcedmsg;
                rm.preReceiveMsgFree = freemsg;
                if (forcedmsg.Count == 0)
                {
                    //random set value
                    if((rm as Proposer).proposeValue == -1)
                    {
                        (rm as Proposer).proposeValue = Util.randRgb();
                    }
                    Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.step2a, sendedMsgId, (rm as Proposer).proposeValue);
                    List<Acceptor> acceptors = rm.parent.getAllAcceptor();
                    foreach (Acceptor i in acceptors)
                    {
                        rm.send(sendMsg, i.connector.localPort);
                    }
                    ProposerWait2bState res = new ProposerWait2bState(rm, sendedMsgId, (rm as Proposer).proposeValue);
                    return res;
                }else
                {
                    int u = 0;
                    foreach (Msg m in forcedmsg)
                    {
                        //becareful,it should use max2b instead of msgId,because all msg Id is the current largest,but max2b is not
                        u = Math.Max(u, m.max2b);
                    }
                    int v = -1;
                    foreach (Msg m in forcedmsg)
                    {
                        if (u == m.max2b)
                        {
                            if (v == -1 || v == m.data)
                            {
                                v = m.data;
                            }
                            else
                            {
                                Logger.log("error, step1bForced has mutil value" + rm.ToString() + " msg:" + msg.ToString());
                            }
                        }
                    }
                    //send receive value
                    Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.step2a, sendedMsgId, v);
                    List<Acceptor> acceptors = rm.parent.getAllAcceptor();
                    foreach (Acceptor i in acceptors)
                    {
                        rm.send(sendMsg, i.connector.localPort);
                    }
                    ProposerWait2bState res = new ProposerWait2bState(rm, sendedMsgId, v);
                    return res;
                }
            }
            return this;
        }

        public override State execute(Msg msg)
        {
            if (msg == null)
            {
                //simulate corrupt
                if (Util.randCorrupt(rm))
                {
                    CorruptState state = new CorruptState(rm, this, Util.randCorruptTime() + Paxos.getTime());
                    return state;
                }
                if (createTime + rm.parent.getWait1bMaxTime() < Paxos.getTime())
                {
                    return new ProposerWorkState(rm, sendedMsgId);
                }
                return this;
            }
            else
            {
                if (processDiffEraMsg(msg)|| processDiffMsgId(msg))
                {
                    return this;
                }
                State res = this;
                switch (msg.type)
                {
                    case Msg.typeEnum.step1bFree:
                    case Msg.typeEnum.step1bForced:
                        res = step1b(msg);
                        break;
                    case Msg.typeEnum.eraHasValue:
                        res = eraHasValue(msg);
                        break;
                    case Msg.typeEnum.getEraValue:
                        res = getUnreasonableMsg(msg);
                        break;
                    case Msg.typeEnum.step3:
                        res = eraHasValue(msg);
                        break;
                    default:
                        res = debugProcessRemainMsg(msg);
                        break;
                }
                return res;
            }
        }
    }

    public class ProposerWait2bState : ProposerState
    {
        int sendedValue;
        List<Msg> step2bMsg = new List<Msg>();
        public ProposerWait2bState(RM rm, int sendedMsgId, int sendedValue) : base(rm, sendedMsgId)
        {
            this.sendedValue = sendedValue;
        }
        public State step2b(Msg msg)
        {
            step2bMsg.Add(msg);
            if (getDistinctSource(step2bMsg) > rm.parent.getAllAcceptor().Count / 2)
            {
                //send receive value
                Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.step3, sendedMsgId, sendedValue);
                List<RM> rms = rm.parent.getAllRM();
                rm.parent.determinEraMsg(rm.curEra, sendedValue, rm);
                foreach (RM i in rms)
                {
                    rm.send(sendMsg, i.connector.localPort);
                }
                rm.curEra++;
                //becareful, new round's msgid is init
                //return new ProposerWorkState(rm, sendedMaxMsgId);
                return new ProposerWorkState(rm, -1);
            }
            else
            {
                return this;
            }
        }

        public override State execute(Msg msg)
        {
            if (msg == null)
            {
                //simulate corrupt
                if (Util.randCorrupt(rm))
                {
                    CorruptState state = new CorruptState(rm, this, Util.randCorruptTime() + Paxos.getTime());
                    return state;
                }
                if (createTime + rm.parent.getWait2bMaxTime() < Paxos.getTime())
                {
                    return new ProposerWorkState(rm, sendedMsgId);
                }
                return this;
            }
            else
            {
                if (processDiffEraMsg(msg)|| processDiffMsgId(msg))
                {
                    return this;
                }
                State res = this;
                switch (msg.type)
                {
                    case Msg.typeEnum.step2b:
                        res = step2b(msg);
                        break;
                    case Msg.typeEnum.eraHasValue:
                        res = eraHasValue(msg);
                        break;
                    case Msg.typeEnum.step1bForced:
                    case Msg.typeEnum.step1bFree:
                        //discard the delay msg
                        break;
                    case Msg.typeEnum.step3:
                        res = eraHasValue(msg);
                        break;
                    default:
                        res = debugProcessRemainMsg(msg);
                        break;
                }
                return res;
            }
        }
    }

    public class AcceptorState : State
    {
        //initial received1aMsgId is -1
        public int participateMaxId = -1;
        public int received1aMsgId = -1;
        public bool hasSended2bMsg = false;
        public int sended2bMsgId = -1;
        public int sended2bMsgValue = -1;
        public Msg accept2aMsg = null;
        public AcceptorState(RM rm) : base(rm,-1)
        {
        }

        public void conflict2bMsgValue(int oldvalue, int newvalue)
        {
            if (oldvalue == newvalue)
            {
                return;
            }
            else
            {
                List<RM> rms = rm.parent.getAllRM();
                int cnt = 0;
                List<RM> cntrm = new List<RM>();
                foreach(RM r in rms)
                {
                    if(r.state is AcceptorState)
                    {
                        AcceptorState s = r.state as AcceptorState;
                        if(s.sended2bMsgValue == oldvalue)
                        {
                            cnt++;
                            cntrm.Add(r);
                        }
                        if (cnt > rm.parent.getAllAcceptor().Count/2) {
                            Logger.log("error!conflict value");
                        }
                    }
                }

                return;
            }
        }

        public State step1a(Msg msg)
        {
            if (msg.msgId >= participateMaxId)
            {
                Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.step1bFree, msg.msgId, -1);
                sendMsg.max1a = received1aMsgId;
                if (hasSended2bMsg)
                {
                    sendMsg.type = Msg.typeEnum.step1bForced;
                    sendMsg.max2b = sended2bMsgId;
                    sendMsg.data = sended2bMsgValue;
                }
                rm.send(sendMsg, msg.sourcePort);
                received1aMsgId = Math.Max(received1aMsgId, msg.msgId);
                participateMaxId = Math.Max(participateMaxId, msg.msgId);
            }else
            {
                willnotReplyMsg(msg);
            }
            return this;
        }

        public State step2a(Msg msg)
        {
            if (msg.msgId >= participateMaxId)
            {
                Msg sendMsg = new Msg(rm.connector.localPort, rm.curEra, Msg.typeEnum.step2b, msg.msgId, -1);
                sendMsg.max1a = received1aMsgId;
                if (hasSended2bMsg)
                {
                    conflict2bMsgValue(sended2bMsgValue, msg.data);
                    sendMsg.max2b = sended2bMsgId;
                    //sendMsg.data = sended2bMsgValue;
                }
                rm.send(sendMsg, msg.sourcePort);
                accept2aMsg = msg;
                participateMaxId = Math.Max(participateMaxId, msg.msgId);
                hasSended2bMsg = true;
                sended2bMsgId = Math.Max(sended2bMsgId, msg.msgId);
                sended2bMsgValue = msg.data;
            }else
            {
                willnotReplyMsg(msg);
            }
            return this;
        }

        public State step3(Msg msg)
        {
            if (msg.era < rm.curEra)
            {
                return this;
            }
            rm.curEra++;
            return new AcceptorState(rm);
        }

        public State eraHasValue(Msg msg)
        {
            if (msg.era < rm.curEra)
            {
                return this;
            }
            rm.curEra++;
            return new AcceptorState(rm);
        }

        override
        public State execute(Msg msg)
        {
            if (msg == null)
            {
                //simulate corrupt
                if (Util.randCorrupt(rm))
                {
                    CorruptState state = new CorruptState(rm, this, Util.randCorruptTime() + Paxos.getTime());
                    return state;
                }
            }
            else
            {
                if (processDiffEraMsg(msg))
                {
                    return this;
                }
                State res = this;
                switch (msg.type)
                {
                    case Msg.typeEnum.eraHasValue:
                        res = eraHasValue(msg);
                        break;
                    case Msg.typeEnum.getEraValue:
                        res = getEraValue(msg);
                        break;
                    //case Msg.typeEnum.get1aMax:
                    //    res = get1aMax(msg);
                    //    break;
                    case Msg.typeEnum.step1a:
                        res = step1a(msg);
                        break;
                    case Msg.typeEnum.step2a:
                        res = step2a(msg);
                        break;
                    case Msg.typeEnum.step3:
                        res = step3(msg);
                        break;
                    default:
                        res = debugProcessRemainMsg(msg);
                        break;
                }
                return res;
            }
            return this;
        }
    }

    public class Paxos
    {
        //use to draw ui
        Canvas canvas;
        //use to print status text
        TextBlock txtStatus;
        //all character
        public List<Shape> rms = new List<Shape>();
        //every era's determined data{era,data}
        private Dictionary<int, int> eraData = new Dictionary<int, int>();
        //if determined data has conflication, store them{era,error message}
        private Dictionary<int, List<object>> errorEraMsg = new Dictionary<int, List<object>>();
        //every port to the shape entity
        private Dictionary<int, Shape> portToRMDict = new Dictionary<int, Shape>();

        public int getEraValue(int era)
        {
            return eraData[era];
        }

        public static int arrowThinessTimeSlide()
        {
            return 2;
        }

        public static int arrowMaxThiness()
        {
            return 5;
        }

        public int getAllRmCount()
        {
            return portToRMDict.Count;
        }

        public List<RM> getAllRM()
        {
            List<RM> res = new List<RM>();
            foreach (var it in portToRMDict)
            {
                if (it.Value.Tag is RM)
                {
                    res.Add(it.Value.Tag as RM);
                }
            }
            return res;
        }

        int []proposeCnt = new int[10];
        Dictionary<int, int> usedMsgId = new Dictionary<int, int>();

        public void determinEraMsg(int era, int value,RM rm)
        {
            if (eraData.ContainsKey(era))
            {
                if (eraData[era] != value)
                {
                    if (!errorEraMsg.ContainsKey(era))
                    {
                        errorEraMsg.Add(era, new List<object>());
                        Logger.error("error:era have different value,pre :" + eraData[era] + " after: " + value);
                    }
                    errorEraMsg[era].Add(value);
                }
                else
                {
                    Logger.log("duplicate determine era " + era + " value " + value);
                }
            }
            else
            {
                List<Proposer> proposers = rm.parent.getAllProposer();
                int flag = 0;
                foreach(Proposer p in proposers)
                {
                    if( p.proposeValue == value)
                    {
                        Logger.log("Proposer " + p.id + " propose era " + era + " value " + value+" and rm "+rm.id+" determine");
                        flag = 1;
                        if (p.id != rm.id)
                        {
                            Logger.log("a succed rm receice the preview value");
                        }
                    }
                }
                eraData[era] = value;
                if (flag == 0)
                {
                    Logger.error("a not proposed value is propose");
                }
                proposeCnt[rm.id]++;
                if (!usedMsgId.ContainsKey(rm.state.sendedMsgId))
                {
                    usedMsgId[rm.state.sendedMsgId] = 0;
                }
                usedMsgId[rm.state.sendedMsgId]++;
            }
        }

        public RM portToRM(int port)
        {
            return portToRMDict[port].Tag as RM;
        }

        public long getWaitWorkMaxTime()
        {
            return 6;
        }

        public long getWait1bMaxTime()
        {
            return 6;
        }

        public long getWait2bMaxTime()
        {
            return 8;
        }

        //public List<int> getAllAcceptor()
        //{
        //    List<int> res = new List<int>();
        //    foreach (var it in portToRMDict)
        //    {
        //        if (it.Value.Tag is Acceptor)
        //        {
        //            res.Add(it.Key);
        //        }
        //    }
        //    return res;
        //}

        public List<Acceptor> getAllAcceptor()
        {
            List<Acceptor> res = new List<Acceptor>();
            foreach (var it in portToRMDict)
            {
                if (it.Value.Tag is Acceptor)
                {
                    res.Add(it.Value.Tag as Acceptor);
                }
            }
            return res;
        }


        public List<Proposer> getAllProposer()
        {
            List<Proposer> res = new List<Proposer>();
            foreach (var it in portToRMDict)
            {
                if (it.Value.Tag is Proposer)
                {
                    res.Add(it.Value.Tag as Proposer);
                }
            }
            return res;
        }

        public Acceptor getRandAcceptor()
        {
            List<Acceptor> acceptors = getAllAcceptor();
            return acceptors[Util.rnd.Next(0, acceptors.Count)];
        }

        bool stopped = false;

        public void init(Canvas canvas, TextBlock txtStatus)
        {
            //init data
            this.canvas = canvas;
            this.txtStatus = txtStatus;
            int id = 0;
            int max = 6;//determine all rm count
            double radiu = 200;//round of ui size
            double centerX = 250, centerY = 250;//ui center point
            for (int i = 0; i < max; i += 1, id += 1)
            {
                Shape shape;
                RM rm;
                if (id < max / 2)
                {
                    rm = new Acceptor();
                    rm.parent = this;
                    rm.id = id;
                    rm.x = (int)(centerX + radiu * Math.Cos(2 * Math.PI * (double)id / max));
                    rm.y = (int)(centerY + radiu * Math.Sin(2 * Math.PI * (double)id / max));

                    rm.rgb[0] = rm.rgb[1] = rm.rgb[2] = 100;
                    shape = new Ellipse()
                    {
                        Width = 20,
                        Height = 20,
                        Stroke = new SolidColorBrush(Color.FromRgb(rm.rgb[0], rm.rgb[1], rm.rgb[2])),
                        StrokeThickness = 6
                    };
                    shape.Tag = rm;
                }
                else
                {
                    rm = new Proposer();
                    rm.parent = this;
                    rm.id = id;
                    rm.x = (int)(centerX + radiu * Math.Cos(2 * Math.PI * (double)id / max));
                    rm.y = (int)(centerY + radiu * Math.Sin(2 * Math.PI * (double)id / max));

                    rm.rgb[0] = rm.rgb[1] = rm.rgb[2] = 200;
                    shape = new Rectangle()
                    {
                        Width = 20,
                        Height = 20,
                        Stroke = new SolidColorBrush(Color.FromRgb(rm.rgb[0], rm.rgb[1], rm.rgb[2])),
                        StrokeThickness = 6
                    };
                    shape.Tag = rm;
                }
                canvas.Children.Add(shape);
                rms.Add(shape);
                portToRMDict.Add((shape.Tag as RM).connector.localPort, shape);
                shape.SetValue(Canvas.LeftProperty, (double)rm.x);
                shape.SetValue(Canvas.TopProperty, (double)rm.y);
            }

            if (Util.displayUI())
            {
                //timer start
                System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                dispatcherTimer.Tick += paxosTimer;
                dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
                dispatcherTimer.Start();
                Connector.startConsumeThread();
            }
            else
            {
                console();
                Thread thread = new Thread(new ThreadStart(console));
                thread.Start();
            }

        }

        void console()
        {
            for (;;)
            {
                curTime++;
                paxosAct();
                Connector.consumeCurrent();
            }
        }

        public void stop()
        {
            stopped = true;
        }

        public void resume()
        {
            stopped = false;
        }

        public void paxosAct()
        {
            List<RM> rm = new List<RM>();
            foreach (Shape i in rms)
            {
                rm.Add((i.Tag as RM));
            }
            foreach (RM i in rm)
            {
                i.act();
            }
        }

        static long curTime = 0;
        static public long getTime()
        {
            return curTime;
        }

        private void paxosTimer(object sender, EventArgs e)
        {
            if (stopped)
                return;
            curTime++;
            paxosAct();
            Render.render(canvas.Children, canvas);
            string status = "";
            foreach (UIElement it in canvas.Children)
            {
                if (it is Ellipse || it is Rectangle)
                {
                    if((it as Shape).Name != "")
                    {
                        continue;
                    }
                    RM rm = ((it as Shape).Tag as RM);
                    if(rm is Acceptor)
                    {
                        status += "Acceptor id:" + rm.id + " ";
                    }else
                    {
                        status += "Proposal id:" + rm.id + " ";
                    }
                    status += (rm.state.GetType().Name)+ '\n';

                    if (rm.state is AcceptorState)
                    {
                        AcceptorState acceptor = (rm.state as AcceptorState);
                        status += "  accept (max msgid:" + acceptor.participateMaxId+ ")(1a maxid:" + acceptor.received1aMsgId + ") (2a maxid:" + acceptor.sended2bMsgId+") era:" + rm.curEra + "\n";
                    }else if(rm.state is ProposerState)
                    {
                        ProposerState proposer = (rm.state as ProposerState);
                        status += "  proposer sended message id:" + proposer.sendedMsgId +" data:"+(rm as Proposer).proposeValue + " "+ " era:"+rm.curEra + "\n";
                    }
                }
            }
            txtStatus.Text = status;
        }
    }
}

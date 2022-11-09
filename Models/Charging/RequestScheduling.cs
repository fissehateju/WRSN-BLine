using RR.Computations;
using RR.Dataplane;
using RR.Intilization;
using RR.Comuting.Routing;
using RR.Models.Mobility;
using RR.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using RR.Dataplane.NOS;
using RR.Cluster;

namespace RR.Models.Charging
{
        public class TobeChargedSorters : IComparer<ChargingPriorityEntry>
        {
            public int Compare(ChargingPriorityEntry y, ChargingPriorityEntry x)
            {
                return y.Priority.CompareTo(x.Priority);
            }
        }
    public class ChargingPriorityEntry
    {
        public double Priority { get; set; }
        public double distance { get; set; }
        public double angle { get; set; }
        public int SensorID { get { return Request.Source.ID; } }
        public Packet Request { get; set; }

    }
    public class RequestScheduling
    {
        private BaseStation BStation;
        private Sink TheSink;
        private List<Packet> RequestList;
        private Queue<Packet> SortedReqs = new Queue<Packet>();
        public RequestScheduling()
        {

        }
        public RequestScheduling(BaseStation Bs, Sink Sk, List<Packet> Reqs)
        {
            BStation = Bs;
            TheSink = Sk;   
            RequestList = Reqs;
        }

        public Metrics NWmetrics;
        public Queue<Packet> reOrdering(ClusteringForRECO terr)
        {
            ////// 
            ///// Travelling Salesman algorithm to find the efficient path for Mobile charger traveling

            //var tsmRout = new StartTSM();
            //List<int> orderdID = tsmRout.Startit(RequestList);
            //int indexOFzero;
            //List<int> temp;
            //for (int i = 0; i < orderdID.Count; i++)
            //{
            //    if (orderdID[0] != 0)  // rearrange the list if indexzero doesnt contain ID of node zero.
            //    {
            //        temp = new List<int>();
            //        indexOFzero = orderdID.IndexOf(0);
            //        temp.AddRange(orderdID.GetRange(0, indexOFzero));
            //        orderdID.RemoveRange(0, indexOFzero);
            //        orderdID.AddRange(temp);
            //        break;
            //    }
            //}

            //orderdID.Remove(0); // node zero is not needed
            //while (orderdID.Count > 0)
            //{
            //    foreach (Packet p in RequestList)
            //    {
            //        if (p.Source.ID == orderdID[0])
            //        {
            //            SortedReqs.Enqueue(p);
            //            orderdID.RemoveAt(0);
            //            break;
            //        }
            //    }
            //}

            Packet closestPack;
            Point MCP = TheSink.CenterLocation;
            while (RequestList.Count > 0)
            {
                closestPack = getClosestNode(MCP, RequestList);
                SortedReqs.Enqueue(closestPack);
                MCP = closestPack.Source.CenterLocation;
                RequestList.Remove(closestPack);
            }

            return SortedReqs;
        }
        

        public Packet getClosestNode(Point MC_loc, List<Packet> reqs)
        {
            double closer = double.MaxValue;
            Packet holder = reqs[0];

            foreach (Packet reqPacket in reqs)
            {
                double Ds_next = Operations.DistanceBetweenTwoPoints(MC_loc, reqPacket.Source.CenterLocation);

                if (Ds_next < closer)
                {
                    closer = Ds_next;
                    holder = reqPacket;
                }
            }
            return holder;
        }

        
    }
}

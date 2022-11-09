using MP.MergedPath.Routing;
using RR.Dataplane;
using RR.Dataplane.NOS;
using RR.Intilization;
using RR.Comuting.computing;
using System;
using System.Collections.Generic;

namespace RR.Comuting.Routing
{
    

    class DataPacketMessages
    {
        private LoopMechanizimAvoidance loopMechan = new LoopMechanizimAvoidance();
        private NetworkOverheadCounter counter;
        /// <summary>
        /// currentBifSensor: current node that has the packet.
        /// Branches: the branches 
        /// isSourceAnAgent: the source is an agent for a sink. That is to say no need for clustering the source itslef this time.
        /// </summary>
        /// <param name="currentBifSensor"></param>
        /// <param name="Branches"></param>
        /// <param name="packet"></param>
        /// <param name="isSourceAnAgent"></param>
        public DataPacketMessages(Sensor sender, Packet packet)
        {
            counter = new NetworkOverheadCounter();

            // create new:

            //skip the test here and send to the known sink by urself
            //Hand of to the sink by urself 
            
            if (packet != null)
            {
                Packet pkt = GeneragtePacket(sender, packet.agentsRowT); //                                                         

                if (pkt.Destination == null)
                {
                    pkt.Destination = sender.clusterHeader; // PublicParamerters.MainWindow.myNetWork[0];
                }

                pkt.TimeToLive = Convert.ToInt16((Operations.DistanceBetweenTwoPoints(sender.CenterLocation, pkt.Destination.CenterLocation) / (PublicParamerters.CommunicationRangeRadius / 2)));
                pkt.TimeToLive += PublicParamerters.HopsErrorRange;

                SendPacket(sender, pkt);
            }
            else
            {
                Packet pkt = GeneragtePacket(sender, sender.agentRow); //                                                         

                if (pkt.Destination == null)
                {
                    pkt.Destination = sender.clusterHeader; // PublicParamerters.MainWindow.myNetWork[0];
                }

                pkt.TimeToLive = Convert.ToInt16((Operations.DistanceBetweenTwoPoints(sender.CenterLocation, pkt.Destination.CenterLocation) / (PublicParamerters.CommunicationRangeRadius / 3)));
                pkt.TimeToLive += PublicParamerters.HopsErrorRange;

                if (sender.ID == pkt.Destination.ID)
                {
                    //HandOffToTheSinkOrRecovry(sender, pkt);
                    sender.collectedDataPackets.Enqueue(pkt);
                }
                else
                {
                    SendPacket(sender, pkt);
                }
            }
                                            
        }
        
    

        public DataPacketMessages()
        {
            counter = new NetworkOverheadCounter();
        }


        public void HandelInQueuPacket(Sensor currentNode, Packet InQuepacket)
        {
            SendPacket(currentNode, InQuepacket);
        }




        /// <summary>
        /// duplicate the packet. this means no new packet is generated.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="currentBifSensor"></param>
        /// <returns></returns>
        private Packet Duplicate(Packet packet, Sensor currentBifSensor)
        {
            Packet pck = packet.Clone() as Packet;
            return pck;
        }
      
        private Sensor determinaDestination(Sensor sender, AgentsRow agents)
        {
            if(agents == null)
            {
                //return PublicParamerters.MainWindow.myNetWork[0];
                return sender.clusterHeader;
            }

            DateTime currentdate = DateTime.Now;
            int s = currentdate.Second;
            int m = currentdate.Minute;
            int h = currentdate.Hour;
            int d = currentdate.Day;

            TimeSpan currentTime = new TimeSpan(h, m, s);
            currentTime = currentdate.Subtract(currentdate.Date); // also possible

            Sensor BsNode = PublicParamerters.MainWindow.myNetWork[0];
            Sensor possibleDest = null;

            foreach (var agent in agents.agentAtTimeT)
            {
                ////this if condition increase delay                
                if (TimeSpan.Compare(currentTime, agent.Value) <= 0 &&
                    Operations.DistanceBetweenTwoPoints(sender.CenterLocation, agent.Key.CenterLocation) <
                    Operations.DistanceBetweenTwoPoints(sender.CenterLocation, sender.clusterHeader.CenterLocation)) //// -1 if t1 is shorter than t2. 0 if  t1 is equal to t2. 1 if  t1 is longer than t2.
                {
                    possibleDest = agent.Key;
                }

                ////third option
                //int hdif = currentdate.Hour - agent.Value.Hours;
                //int mdif = currentdate.Minute - agent.Value.Minutes;
                //int sdif = currentdate.Second - agent.Value.Seconds;

                //if (hdif == 0 && mdif == 0 && (sdif >= -50 && sdif <= 10)) //// -1 if t1 is shorter than t2. 0 if  t1 is equal to t2. 1 if  t1 is longer than t2.
                //{
                //    ans = true;
                //}
                //else
                //{ 
                //    sensorList.Add(agent.Key);
                //}
            }
            
            return possibleDest;
        }

        private Packet GeneragtePacket(Sensor sender, AgentsRow row)
        {
            //Should not enter here if its an agent
            PublicParamerters.NumberofGeneratedPackets += 1;
            Packet pck = new Packet();
            pck.Source = sender;
            pck.Path = "" + sender.ID;
            pck.ReTransmissionTry = 0;
            pck.Destination = sender.clusterHeader; // determinaDestination(sender, row);
            pck.PacketType = PacketType.Data;
            pck.generateTime = DateTime.Now;
            pck.PID = PublicParamerters.NumberofGeneratedPackets;           
            counter.DisplayRefreshAtGenertingPacket(sender, PacketType.Data);
            return pck;
        }

        public void SendPacket(Sensor sender, Packet pck)
        {

            HandOffToTheSinkOrRecovry(sender, pck);

            if (pck.isDelivered)
            {
                return;
            }
            else
            {
                // neext hope:
                sender.SwichToActive(); // switch on me.
                Sensor Reciver = SelectNextHop(sender, pck);
                if (Reciver != null)
                {
                    // overhead:
                    counter.ComputeOverhead(pck, EnergyConsumption.Transmit, sender, Reciver);
                    counter.Animate(sender, Reciver, pck);
                    //:
                    Reciver.numOfPacketsPassingThrough += 1;
                    RecivePacket(Reciver, pck);
                }
                else
                {
                    counter.SaveToQueue(sender, pck); // save in the queue.
                }
            }                     
        }

        private void RecivePacket(Sensor Reciver, Packet packt)
        {
            packt.Path += ">" + Reciver.ID;

            if (loopMechan.isLoop(packt))
            {
                counter.DropPacket(packt, Reciver, PacketDropedReasons.Loop);
            }
            else
            {
                packt.ReTransmissionTry = 0;
                if (packt.Destination.ID == Reciver.ID)
                {
                    counter.ComputeOverhead(packt, EnergyConsumption.Recive, null, Reciver);

                    Reciver.collectedDataPackets.Enqueue(packt);
                    //HandOffToTheSinkOrRecovry(Reciver, packt);
                }
                else
                {
                    counter.ComputeOverhead(packt, EnergyConsumption.Recive, null, Reciver);
                    if (packt.Hops <= packt.TimeToLive)
                    {
                        SendPacket(Reciver, packt);
                    }
                    else
                    {
                        counter.DropPacket(packt, Reciver, PacketDropedReasons.TimeToLive);
                    }
                }
            }
        }
  


        /// <summary>
        /// find x in inlist
        /// </summary>
        /// <param name="x"></param>
        /// <param name="inlist"></param>
        /// <returns></returns>
        private bool Find(SinksAgentsRow x, List<SinksAgentsRow> inlist)
        {
            foreach (SinksAgentsRow rec in inlist)
            {
                if (rec.Sink.ID == x.Sink.ID)
                {
                    return true;
                }
            }

            return false;
        }

        //private List<SinksAgentsRow> GetMySinksFromPacket(Sensor Agent, Packet pck)
        //{
        //    int AgentID = Agent.ID;
        //    bool isFollowup = pck.isFollowUp;
        //    List<SinksAgentsRow> inpacketSinks = pck.SinksAgentsList;

        //    List<SinksAgentsRow> re = new List<SinksAgentsRow>();
        //    foreach (SinksAgentsRow x in inpacketSinks)
        //    {
        //        if (x.AgentNode.ID == AgentID)
        //        {
        //            re.Add(x);
        //        }
        //    }
        //    return re;
        //}

        //private SinksAgentsRow GetMySinksFromPacket(Sensor Agent, Packet pck)
        //{
        //    List<SinksAgentsRow> inpacketSinks = pck.SinksAgentsList;

        //    SinksAgentsRow re = null;
        //    foreach (SinksAgentsRow x in inpacketSinks)
        //    {
        //        if (x.AgentNode.ID == Agent.ID)
        //        {
        //            re = x;
        //        }
        //    }
        //    return re;
        //}
        //private SinksAgentsRow GetMyclosestSinkFromPacket(Sensor Agent, Packet pck)
        //{
        //    List<SinksAgentsRow> inpacketSinks = pck.SinksAgentsList;

        //    SinksAgentsRow re = null;
        //    foreach (SinksAgentsRow x in inpacketSinks)
        //    {
        //        if (x.AgentNode.ID == Agent.ID)
        //        {
        //            re = x;
        //        }
        //    }
        //    return re;
        //}

        /// <summary>
        /// hand the packet to my sink.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="packt"></param>
        /// 
        
        private void HandOffToTheSinkOrRecovry(Sensor agent, Packet packt)
        {
            
            if (agent.ID == 0)
            {
                PublicParamerters.TotalNumDatacollectedBS += 1;
                packt.Path += "> BaseStation 0";
                counter.SuccessedDeliverdPacket(packt);
            }                               
            else if (agent.agentRow != null && Operations.DistanceBetweenTwoPoints(agent.CenterLocation, agent.agentRow.Sink.CenterLocation) <= PublicParamerters.CommunicationRangeRadius)
            {
                packt.Path += "> Sink : " + agent.agentRow.Sink.ID;
                counter.SuccessedDeliverdPacket(packt);
            }
            else if (Operations.DistanceBetweenTwoPoints(agent.CenterLocation, PublicParamerters.MainWindow.myNetWork[0].CenterLocation) <= PublicParamerters.CommunicationRangeRadius)
            {
                PublicParamerters.TotalNumDatacollectedBS += 1;
                packt.Path += "> BaseStation 1";
                counter.SuccessedDeliverdPacket(packt);
            }
           
        }


   
        
        /// <summary>
        /// get the max value
        /// </summary>
        /// <param name="ni"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public Sensor SelectNextHop(Sensor ni, Packet packet)
        {
                return new GreedyRoutingMechansims().RrGreedy(ni, packet);
        }


    }
}

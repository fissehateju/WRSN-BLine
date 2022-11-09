using MP.MergedPath.Routing;
using RR.Dataplane;
using RR.Dataplane.NOS;
using RR.Intilization;
using RR.Comuting.computing;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows;

namespace RR.Comuting.Routing
{
    /// <summary>
    /// from a hightier node to a given source
    /// </summary>
    class ResonseSinkPositionMessage
    {
        private LoopMechanizimAvoidance loopMechan = new LoopMechanizimAvoidance();
        private NetworkOverheadCounter counter;

        /// <summary>
        ///  the hightierNode should response to the lowtiernode. 
        ///  here the respnse should contain all sinks.
        /// </summary>
        /// <param name="hightierNode"></param>
        /// <param name="lowtiernode"></param>
        public ResonseSinkPositionMessage(Sensor hightierNode, Sensor lowtiernode)
        {
            counter = new NetworkOverheadCounter();
            // the hightierNode=lowtiernode --> means that the high tier node itself has data to send.

            if(hightierNode.agentRow != null && !hightierNode.isAgentAvailable())
            {
                hightierNode.agentRow.agentAtTimeT.Clear();
            }

            if (hightierNode.ID == lowtiernode.ID)
            {
                // here high tier has data to send.
               Packet responspacket = GeneratePacket(hightierNode, hightierNode, null,false);
                counter.SuccessedDeliverdPacket(responspacket); //hightierNode
                PreparDataTransmission(hightierNode, responspacket);
            }
            else
            {
                Packet responspacket = GeneratePacket(hightierNode, lowtiernode, null,true);
                SendPacket(hightierNode, responspacket);
            }
        }

        public Packet GeneratePacket(Sensor hightierNode, Sensor lowtiernode, List<int> SinkIDs, bool IncreasePid)
        {
            if (IncreasePid)
            {

                // normal ResponseSinkPosition:
                PublicParamerters.NumberofGeneratedPackets += 1;
                Packet pck = new Packet();
                pck.Source = hightierNode;
                pck.Path = "" + hightierNode.ID;
                pck.Destination = lowtiernode;
                pck.agentsRowT = hightierNode.agentRow;
                pck.PacketType = PacketType.ResponseSinkPosition;
                pck.PID = PublicParamerters.NumberofGeneratedPackets;
                pck.TimeToLive = Convert.ToInt16((Operations.DistanceBetweenTwoPoints(hightierNode.CenterLocation, lowtiernode.CenterLocation) / (PublicParamerters.CommunicationRangeRadius / 3)));
                pck.TimeToLive += PublicParamerters.HopsErrorRange;
                counter.DisplayRefreshAtGenertingPacket(hightierNode, PacketType.ResponseSinkPosition);
                return pck;
            }
            else
            {

                // normal ResponseSinkPosition:
                Packet pck = new Packet();
                pck.SinkIDsNeedsRecovery = null;
                pck.Source = hightierNode;
                pck.Path = "" + hightierNode.ID;
                pck.Destination = lowtiernode;
                pck.agentsRowT = hightierNode.agentRow;
                pck.PacketType = PacketType.ResponseSinkPosition;
                pck.PID = PublicParamerters.NumberofGeneratedPackets;
                pck.TimeToLive = Convert.ToInt16((Operations.DistanceBetweenTwoPoints(hightierNode.CenterLocation, lowtiernode.CenterLocation) / (PublicParamerters.CommunicationRangeRadius / 3)));
                pck.TimeToLive += PublicParamerters.HopsErrorRange;
                counter.DisplayRefreshAtGenertingPacket(hightierNode, PacketType.ResponseSinkPosition);
                return pck;
            }
        }


        public ResonseSinkPositionMessage()
        {
            counter = new NetworkOverheadCounter();
        }

        public void HandelInQueuPacket(Sensor currentNode, Packet InQuepacket)
        {
            SendPacket(currentNode, InQuepacket);
        }

        public void SendPacket(Sensor sender, Packet pck)
        {
            if (pck.PacketType == PacketType.ResponseSinkPosition)
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
                    counter.SaveToQueue(sender, pck);
                }
            }
        }


        private void PreparDataTransmission(Sensor source, Packet packt)
        {
            if (packt.isRecovery)
            {
                // durig recovery, the agent should not be a source.
                // source should add a record.
                List<Sensor> NewAents = new List<Sensor>(); // new agent for the recovery.
                //foreach (SinksAgentsRow row in packt.SinksAgentsList)
                //{
                    if (packt.SinksAgentsList.AgentNode.ID != source.ID)
                    {
                        bool isFound = Operations.FindInAlistbool(packt.SinksAgentsList.AgentNode, NewAents);
                        if (!isFound)
                        {
                            // agent row.AgentNode is for the sink row.sink.
                            NewAents.Add(packt.SinksAgentsList.AgentNode);
                        }
                    }
                //}

                if (source.RecoveryRow == null)
                {
                    Console.WriteLine("ResonseSinkPositionMessage. New Recovery Record is created at prevAgent ID" + source.ID + " Packet PID " + packt.PID + " Path: " + packt.Path);
                    // keep recored for the recovery of upcoming packets. no need for request and respanse a gain. 
                    source.RecoveryRow = new RecoveryRow()
                    {
                        ObtiantedTime = PublicParamerters.SimulationTime,
                        PrevAgent = source,
                        RecoveryAgentList = packt.SinksAgentsList,
                        ObtainPacketsRetry = 0
                    };
                }
                else
                {
                    Console.WriteLine("ResonseSinkPositionMessage: PrevAgent ID " + source.ID + " has recovery record Packet PID " + packt.PID + " Path:" + packt.Path);
                    source.RecoveryRow.ObtiantedTime = PublicParamerters.SimulationTime;
                    source.RecoveryRow.PrevAgent = source;
                    source.RecoveryRow.RecoveryAgentList = packt.SinksAgentsList;
                    source.RecoveryRow.ObtainPacketsRetry += 1;
                }



                if (NewAents.Count > 0)
                {
                    new DataPacketMessages(source, packt);
                }
                else
                {
                    if (packt.SinkIDsNeedsRecovery != null)
                    {
                        new RecoveryMessage(source, packt); // obtain
                    }
                    else
                    {
                        //Console.WriteLine("ResonseSinkPositionMessage: ******************************* PrevAgent ID " + source.ID + " has recovery record Packet PID " + packt.PID + " Path:" + packt.Path);
                        //MessageBox.Show("xxx&&&&&&&&&&&ResonseSinkPositionMessage");
                    }
                }
            }
            else
            {
                // normal delveiry
                new DataPacketMessages(source, packt);
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

                if (Reciver.ID == packt.Destination.ID) // packet is recived.
                {
                    counter.SuccessedDeliverdPacket(packt);
                    counter.ComputeOverhead(packt, EnergyConsumption.Recive, null, Reciver);
                    counter.DisplayRefreshAtReceivingPacket(Reciver);
                    Reciver.Ellipse_nodeTypeIndicator.Fill = Brushes.Transparent;

                    Reciver.agentRow = packt.agentsRowT;

                    PreparDataTransmission(Reciver, packt);

                }
                else
                {
                    // compute the overhead:
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


        public Sensor SelectNextHop(Sensor ni, Packet packet)
        {
            return new GreedyRoutingMechansims().RrGreedy(ni, packet);
        }



    }
}

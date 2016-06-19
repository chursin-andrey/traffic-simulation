using System;
using System.Collections.Generic;
using System.Text;

namespace CityTrafficSimulator
{
    /// <summary>
    /// Wegroute к узлу назначения. Возвращаемый тип алгоритма *
    /// </summary>
    public class Routing : IEnumerable<Routing.RouteSegment>
    {
        /// <summary>
        /// Wegroute
        /// </summary>
        private LinkedList<RouteSegment> route;

        /// <summary>
        /// Стоимость всего маршрута
        /// </summary>
        public double costs;

        /// <summary>
        /// Количество требуемых изменений переулка
        /// </summary>
        public int countOfLineChanges;


        /// <summary>
        /// Standardkonstruktor (Конструктор по умолчанию) создает новый пустой Wegroute к узлу назначения
        /// </summary>
        public Routing()
        {
            route = new LinkedList<RouteSegment>();
            costs = 0;
            countOfLineChanges = 0;
        }


        /// <summary>
        /// Заносит пройденный RouteSegment в стек маршрута и обновляет стоимость и количество необходимых перестроений
        /// </summary>
        /// <param name="rs">вставляемый RouteSegment</param>
        public void Push(RouteSegment rs)
        {
            route.AddFirst(rs);
            costs += rs.costs;
            if (rs.lineChangeNeeded)
                ++countOfLineChanges;
        }

        /// <summary>
        /// Забирает верхний элемент из route-Stack и обновляет length-Feld
        /// </summary>
        /// <returns>route.First.Value</returns>
        public RouteSegment Pop()
        {
            if (route.Count > 0)
            {
                RouteSegment rs = route.First.Value;
                costs -= rs.costs;
                if (rs.lineChangeNeeded)
                {
                    --countOfLineChanges;
                }

                route.RemoveFirst();
                return rs;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        ///Возвращает обратно верхний элемент из route-Stack
        /// </summary>
        /// <returns>route.First.Value</returns>
        public RouteSegment Top()
        {
            return route.First.Value;
        }

        /// <summary>
        /// возвращает количество элементов route-Stacks
        /// </summary>
        /// <returns>route.Count</returns>
        public int SegmentCount()
        {
            return route.Count;
        }

        #region IEnumerable<RouteSegment> Member

        /// <summary>
        /// Возвращает перечислитель для петель foreach
        /// </summary>
        /// <returns>route.GetEnumerator()</returns>
        public IEnumerator<RouteSegment> GetEnumerator()
        {
            return route.GetEnumerator();
        }

        /// <summary>
        /// Возвращает перечислитель для петель foreach
        /// </summary>
        /// <returns>route.GetEnumerator()</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return route.GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Вычислить минимальное Евклидово расстояние от startNode к одному из узлов из targetNodes
		/// </summary>
        /// <param name="startNode">Начальный узел из них от расстояния должен быть вычислен</param>
		/// <param name="targetNodes">Список LineNodes для которых должно быть рассчитано расстояние</param>
        /// <returns>минимальное Евклидово расстояние</returns>
		private static double GetMinimumEuklidDistance(LineNode startNode, List<LineNode> targetNodes)
			{
			if (targetNodes.Count > 0)
				{
				double minValue = Vector2.GetDistance(startNode.position, targetNodes[0].position);

				for (int i = 1; i < targetNodes.Count; i++)
					{
					double newDist = Vector2.GetDistance(startNode.position, targetNodes[i].position);
					if (newDist < minValue)
						{
						minValue = newDist;
						}
					}

				return minValue;
				}
			return 0;
			}

		/// <summary>
        /// Проверяет, разрешен ли данный тип транспортного средства на всех NodeConnections, входящих в ln.
		/// </summary>
		/// <param name="ln">проверить LineNode</param>
		/// <param name="type"> проверить тип транспортного средства - Vehicle type</param>
        /// <returns>true, если данный тип транспортного средства разрешен на всех NodeConnections работая в ln.</returns>
		public static bool CheckLineNodeForIncomingSuitability(LineNode ln, Vehicle.IVehicle.VehicleTypes type)
			{
			foreach (NodeConnection nc in ln.prevConnections)
				{
				if (!nc.CheckForSuitability(type))
					return false;
				}
			return true;
			}

		/// <summary>
        /// Рассчитывается кратчайший путь к targetNode и сохраняет это как Stack in WayToGo
		/// Реализация A*-алгогритма свободно согласно Wikipedia
		/// </summary>
		/// <param name="startNode">Начальный узел должен расчитываться из кратчайшего пути</param>
		/// <param name="targetNodes">Liste von Zielknoten к одному из которых кратчайший путь должен быть рассчитан</param>
		/// <param name="vehicleType">Vehicle type</param>
		public static Routing CalculateShortestConenction(LineNode startNode, List<LineNode> targetNodes, Vehicle.IVehicle.VehicleTypes vehicleType)
			{
			PriorityQueue<LineNode.LinkedLineNode, double> openlist = new PriorityQueue<LineNode.LinkedLineNode, double>();
			Stack<LineNode.LinkedLineNode> closedlist = new Stack<LineNode.LinkedLineNode>();
			Routing toReturn = new Routing();
			
			// Инсталяция Open List, die Closed List ещё пуста
			// (приоритет или значение f начального узла незначителен)
			openlist.Enqueue(new LineNode.LinkedLineNode(startNode, null, false), 0);

            // этот цикл будет проходить либо до
			// - нахождения оптимального решения или
			// - установления, что решения не существует
			do
				{
                    // Удалить узел с наименьшим значением F (в этом случае с наибольшим значением) из Open List
				PriorityQueueItem<LineNode.LinkedLineNode, double> currentNode = openlist.Dequeue();

				// была найдена цель?
				if (targetNodes.Contains(currentNode.Value.node))
					{
					// только closedList, чтобы включить в маршрутизацию 
					closedlist.Push(currentNode.Value);
					LineNode.LinkedLineNode endnode = closedlist.Pop();
					LineNode.LinkedLineNode startnode = endnode.parent;
					while (startnode != null)
						{
                            // простой / прямой путь через NodeConnection
						if (!endnode.lineChangeNeeded)
							{
							toReturn.Push(new RouteSegment(startnode.node.GetNodeConnectionTo(endnode.node), endnode.node, false, startnode.node.GetNodeConnectionTo(endnode.node).lineSegment.length));
							}
						// необходимо перестроение в другой ряд
						else
							{
							NodeConnection formerConnection = startnode.parent.node.GetNodeConnectionTo(startnode.node);

							double length = formerConnection.GetLengthToLineNodeViaLineChange(endnode.node) + Constants.lineChangePenalty;
                            // начальным / или конечным узлом смены полосы движения является светофор => штраф, поскольку следует ожидать увеличение трафика
							if ((endnode.node.tLight != null) || (startnode.node.tLight != null))
								length += Constants.lineChangeBeforeTrafficLightPenalty;

							toReturn.Push(new RouteSegment(formerConnection, endnode.node, true, length));

                            // Список дел:	Объяснить: здесь что-то делается дважды - если мне не изменяет память,
                            //			    это так должно быть, а не почему так.  Заблаговременно проанализировать и объяснить
							endnode = startnode;
							startnode = startnode.parent;
							}

						endnode = startnode;
						startnode = startnode.parent;
						}
					return toReturn;
					}

				#region Nachfolgeknoten auf die Open List setzen
				// Nachfolgeknoten auf die Open List setzen
				// überprüft alle Nachfolgeknoten und fügt sie der Open List hinzu, wenn entweder
				// - der Nachfolgeknoten zum ersten Mal gefunden wird oder
				// - ein besserer Weg zu diesem Knoten gefunden wird

				#region nächste LineNodes ohne Spurwechsel untersuchen
				foreach (NodeConnection nc in currentNode.Value.node.nextConnections)
					{
					// prüfen, ob ich auf diesem NodeConnection überhaupt fahren darf
					if (!nc.CheckForSuitability(vehicleType))
						continue;

					LineNode.LinkedLineNode successor = new LineNode.LinkedLineNode(nc.endNode, null, false);
					bool nodeInClosedList = false;
					foreach (LineNode.LinkedLineNode lln in closedlist)
						if (lln.node == successor.node)
							{
							nodeInClosedList = true;
							continue;
							}

					// wenn der Nachfolgeknoten bereits auf der Closed List ist - tue nichts
					if (!nodeInClosedList)
						{
						NodeConnection theConnection = currentNode.Value.node.GetNodeConnectionTo(successor.node);
						// f Wert für den neuen Weg berechnen: g Wert des Vorgängers plus die Kosten der
						// gerade benutzten Kante plus die geschätzten Kosten von Nachfolger bis Ziel
						double f = currentNode.Value.length												// exakte Länge des bisher zurückgelegten Weges
							+ theConnection.lineSegment.length;											// exakte Länge des gerade untersuchten Segmentes

						if (currentNode.Value.countOfParents < 3)										// Stau kostet extra, aber nur, wenn innerhalb
							{																			// der nächsten 2 Connections
							f += theConnection.vehicles.Count * Constants.vehicleOnRoutePenalty;
							}
						f += GetMinimumEuklidDistance(successor.node, targetNodes);						// Minimumweg zum Ziel (Luftlinie)
						f *= 14 / theConnection.targetVelocity;
						f *= -1;


						// gucke, ob der Node schon in der Liste drin ist und wenn ja, dann evtl. rausschmeißen
						bool nodeInOpenlist = false;
						foreach (PriorityQueueItem<LineNode.LinkedLineNode, double> pqi in openlist)
							{
							if (pqi.Value.node == successor.node)
								{
								if (f <= pqi.Priority)
									nodeInOpenlist = true;
								else
									openlist.Remove(pqi.Value); // erst entfernen
								break;
								}
							}

						if (!nodeInOpenlist)
							{
							// Vorgängerzeiger setzen
							successor.parent = currentNode.Value;
							openlist.Enqueue(successor, f); // dann neu einfügen
							}
						}
					}
				#endregion

				#region nächste LineNodes mit Spurwechsel untersuchen

				if (currentNode.Value.parent != null)
					{
					NodeConnection currentConnection = currentNode.Value.parent.node.GetNodeConnectionTo(currentNode.Value.node);
					if (currentConnection != null)
						{
						foreach (LineNode ln in currentConnection.viaLineChangeReachableNodes)
							{
							// prüfen, ob ich diesen LineNode überhaupt anfahren darf
							if (!CheckLineNodeForIncomingSuitability(ln, vehicleType))
								continue;

							// neuen LinkedLineNode erstellen
							LineNode.LinkedLineNode successor = new LineNode.LinkedLineNode(ln, null, true);
							bool nodeInClosedList = false;
							foreach (LineNode.LinkedLineNode lln in closedlist)
								if (lln.node == successor.node)
									{
									nodeInClosedList = true;
									break;
									}

							// wenn der Nachfolgeknoten bereits auf der Closed List ist - tue nichts
							if (!nodeInClosedList)
								{
								// passendes LineChangeInterval finden
								NodeConnection.LineChangeInterval lci;
								currentConnection.lineChangeIntervals.TryGetValue(ln.hashcode, out lci);

								if (lci.length < Constants.minimumLineChangeLength)
									break;

								// f-Wert für den neuen Weg berechnen: g Wert des Vorgängers plus die Kosten der
								// gerade benutzten Kante plus die geschätzten Kosten von Nachfolger bis Ziel
								double f = currentNode.Value.parent.length;										// exakte Länge des bisher zurückgelegten Weges
								f += currentConnection.GetLengthToLineNodeViaLineChange(successor.node);

								// Kostenanteil, für den Spurwechsel dazuaddieren
								f += (lci.length < 2 * Constants.minimumLineChangeLength) ? 2 * Constants.lineChangePenalty : Constants.lineChangePenalty;

								// Anfangs-/ oder Endknoten des Spurwechsels ist eine Ampel => Kosten-Penalty, da hier verstärktes Verkehrsaufkommen zu erwarten ist
								if ((lci.targetNode.tLight != null) || (currentConnection.startNode.tLight != null))
									f += Constants.lineChangeBeforeTrafficLightPenalty;

								f += GetMinimumEuklidDistance(successor.node, targetNodes);						// Minimumweg zum Ziel (Luftlinie)
								f *= -1;


								// gucke, ob der Node schon in der Liste drin ist und wenn ja, dann evtl. rausschmeißen
								bool nodeInOpenlist = false;
								foreach (PriorityQueueItem<LineNode.LinkedLineNode, double> pqi in openlist)
									{
									if (pqi.Value.node == successor.node)
										{
										if (f <= pqi.Priority)
											nodeInOpenlist = true;
										else
											openlist.Remove(pqi.Value); // erst entfernen
										break;
										}
									}

								if (!nodeInOpenlist)
									{
									// Vorgängerzeiger setzen
									successor.parent = currentNode.Value;
									openlist.Enqueue(successor, f); // dann neu einfügen
									}
								}
							}
						}
					}


				#endregion

				#endregion

                // текущий узел в настоящее время досконально изучен
				closedlist.Push(currentNode.Value);
				}
			while (openlist.Count != 0);

			// Пути не найдено - в этом случае оставляем мы машину на уничтожение:
			return toReturn;
			}

		/// <summary>
        /// Часть Wegroute. Или один из endNode текущей NodeConnection, или endNode параллельной NodeConnection, которому необходима смена полосы движения 
		/// </summary>
		public class RouteSegment
			{
			/// <summary>
                /// NodeConnection начинается по данному RouteSegment (закончится он может на другом RouteSegment, если необходима смена полосы движения)
			/// </summary>
			public NodeConnection startConnection;

			/// <summary>
			/// LineNode, как следующий должен подъезжать
			/// </summary>
			public LineNode nextNode;

			/// <summary>
			/// Знак, о необходимости смены полосы
			/// </summary>
			public bool lineChangeNeeded;

			/// <summary>
            /// Стоимость этой части (минимальная длина NodeConnection, плюс любые штрафные расходы на дорогую смену полосы движения)
			/// </summary>
			public double costs;


			/// <summary>
            /// Конструктор по умолчанию создает новый участок маршрута
			/// </summary>
            /// <param name="startConnection">NodeConnection начинается на RouteSegment (закончится он может на другом RouteSegment, если необходима смена полосы движения)</param>
            /// <param name="nextNode">LineNode, как следующий должен подъезжать</param>
            /// <param name="lineChangeNeeded">Знак, о необходимости смены полосы</param>
            /// <param name="costs">Стоимость этой части (минимальная длина NodeConnection, плюс любые штрафные расходы на дорогую смену полосы движения)</param>
			public RouteSegment(NodeConnection startConnection, LineNode nextNode, bool lineChangeNeeded, double costs)
				{
				this.startConnection = startConnection;
				this.nextNode = nextNode;
				this.lineChangeNeeded = lineChangeNeeded;
				this.costs = costs;
				}
			}
		}
	}

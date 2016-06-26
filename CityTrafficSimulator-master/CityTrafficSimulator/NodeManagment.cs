
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

using CityTrafficSimulator.Vehicle;
using CityTrafficSimulator.Tools;

namespace CityTrafficSimulator
	{
	/// <summary>
	/// Класс для управения LineNodes, NodeConnections etc.
	/// </summary>
	public class NodeManagment : ISavable, ITickable
		{
		#region Variablen und Felder

		/// <summary>
		/// все используемые LineNodes
		/// </summary>
		private List<LineNode> _nodes = new List<LineNode>();
		/// <summary>
        /// все используемые LineNodes
		/// </summary>
		public List<LineNode> nodes
			{
			get { return _nodes; }
			set { _nodes = value; }
			}

		/// <summary>
        /// все используемые NodeConnections
		/// </summary>
		private List<NodeConnection> _connections = new List<NodeConnection>();
		/// <summary>
        /// все используемые NodeConnections
		/// </summary>
		public List<NodeConnection> connections
			{
			get { return _connections; }
			set { _connections = value; }
			}

		/// <summary>
		/// Список всех известных Intersections
		/// </summary>
		private List<Intersection> _intersections = new List<Intersection>();
		/// <summary>
        /// Список всех известных Intersections
		/// </summary>
		public List<Intersection> intersections
			{
			get { return _intersections; }
			set { _intersections = value; }
			}

		/// <summary>
        /// Список всех известных сетевых уровней
		/// </summary>
		public List<NetworkLayer> _networkLayers { get; private set; }

		/// <summary>
		/// Наименование данного Layouts
		/// </summary>
		private string m_title;
		/// <summary>
        /// Наименование данного Layouts
		/// </summary>
		public string title
			{
			get { return m_title; }
			set { m_title = value; }
			}


		/// <summary>
		/// Информационный текст Layout
		/// </summary>
		private string _infoText;
		/// <summary>
        /// Информационный текст  Layout
		/// </summary>
		public string infoText
			{
			get { return _infoText; }
			set { _infoText = value; }
			}


		#endregion


		#region Konstruktoren

		/// <summary>
		/// пустой конструктор по умолчанию
		/// </summary>
		public NodeManagment()
			{
			_networkLayers = new List<NetworkLayer>();
			}

		#endregion

		#region Methoden für LineNodes

		/// <summary>
		/// добавляет LineNode 
		/// </summary>
		/// <param name="ln">добавленная LineNode</param>
		public void AddLineNode(LineNode ln)
			{
			nodes.Add(ln);
			InvalidateNodeBounds();
			}


		/// <summary>
		/// удаляет LineNode и все связанные с ним NodeConnections
		/// </summary>
		/// <param name="ln">удаленнаяLineNode</param>
		public void DeleteLineNode(LineNode ln)
			{
			// удалить иходящий NodeConnections
			while (ln.nextConnections.Count > 0)
				{
				Disconnect(ln, ln.nextConnections[0].endNode);
				}

			// удалить входящие NodeConnections
			while (ln.prevConnections.Count > 0)
				{
				Disconnect(ln.prevConnections[0].startNode, ln);
				}

			nodes.Remove(ln);
			InvalidateNodeBounds();
			}


		/// <summary>
		/// устанавливает NodeConnection от from до to
		/// </summary>
		/// <param name="from">LineNode должна исходить от NodeConnection</param>
		/// <param name="to">LineNode должен подходить к NodeConnection</param>
        /// <param name="priority">Приоритет линии</param>
		/// <param name="targetVelocity">Target velocity на NodeConnection</param>
		/// <param name="carsAllowed">Знак, о разрешеннии машин на этом NodeConnection</param>
        /// <param name="busAllowed">Знак, о разрешеннии автобусов на этом NodeConnection</param>
        /// <param name="tramAllowed">Знак, о разрешеннии трамваев на этом NodeConnection</param>
		/// <param name="enableIncomingLineChange">Знак, о разрешении входящей смены полосы движения</param>
        /// <param name="enableOutgoingLineChange">Знак, о разрешении исходящей смены полосы движения</param>
		public void Connect(LineNode from, LineNode to, int priority, double targetVelocity, bool carsAllowed, bool busAllowed, bool tramAllowed, bool enableIncomingLineChange, bool enableOutgoingLineChange)
			{
			NodeConnection nc = new NodeConnection(from, to, null, priority, targetVelocity, carsAllowed, busAllowed, tramAllowed, enableIncomingLineChange, enableOutgoingLineChange);

			TellNodesTheirConnection(nc);
			UpdateNodeConnection(nc);
			ResetAverageVelocities(nc);

			connections.Add(nc);
			}

		/// <summary>
		/// устанавливает nextNodes и prevNodes на nc участвующих LineNodes
		/// </summary>
		/// <param name="nc">NodeConnections должны быть зарегистрированными</param>
		private void TellNodesTheirConnection(NodeConnection nc)
			{
			nc.startNode.nextConnections.Add(nc);
			nc.endNode.prevConnections.Add(nc);
			}

		/// <summary>
		/// отрезает все существующие NodeConnection между from и to и говорит Nodes, а также принамаю с сведение, что они уже не применимы
		/// </summary>
		/// <param name="from">LineNode от которых исходит NodeConnection</param>
		/// <param name="to">LineNode в который входит NodeConnection</param>
		public void Disconnect(LineNode from, LineNode to)
			{
			NodeConnection nc;
			while ((nc = GetNodeConnection(from, to)) != null)
				{
				// Intersections удалить 
				while (nc.intersections.Count > 0)
					{
					DestroyIntersection(nc.intersections.First.Value);
					}

				// LineChangePoints удалить, если они существуют
				RemoveLineChangePoints(nc, true, true);

				// Connections решить и удалить
				from.nextConnections.Remove(nc);
				to.prevConnections.Remove(nc);
				connections.Remove(nc);
				}
			}

		/// <summary>
		/// Обновить все NodeConnections, исходящие из nodeToUpdate
		/// </summary>
		/// <param name="nodeToUpdate">LineNode, чьи исходящие NodeConencitons должны быть обновлены</param>
		public void UpdateOutgoingNodeConnections(LineNode nodeToUpdate)
			{
			foreach (NodeConnection nc in nodeToUpdate.nextConnections)
				{
				UpdateNodeConnection(nc);
				}
			}

		/// <summary>
		/// Обновить все NodeConnections, входящие в nodeToUpdate
		/// </summary>
        /// <param name="nodeToUpdate">LineNode, чьи входящие NodeConencitons должны быть обновлены</param>
		public void UpdateIncomingNodeConnections(LineNode nodeToUpdate)
			{
			foreach (NodeConnection nc in nodeToUpdate.prevConnections)
				{
				UpdateNodeConnection(nc);
				}
			}

		/// <summary>
		/// Обновить все NodeConnections, связанные с nodeToUpdate
		/// </summary>
		/// <param name="nodeToUpdate">LineNode, чьи NodeConencitons должны быть обновлены</param>
		public void UpdateNodeConnections(LineNode nodeToUpdate)
			{
			UpdateIncomingNodeConnections(nodeToUpdate);
			UpdateOutgoingNodeConnections(nodeToUpdate);
			}

		/// <summary>
		/// Обновить все NodeConnections, связанные с LineNodes из nodesToUpdate
		/// </summary>
		/// <param name="nodesToUpdate">Liste LineNoden чьи NodeConencitons должны быть обновлены</param>
		public void UpdateNodeConnections(List<LineNode> nodesToUpdate)
			{
			foreach (LineNode ln in nodesToUpdate)
				{
				UpdateIncomingNodeConnections(ln);
				UpdateOutgoingNodeConnections(ln);
				}
			}

		/// <summary>
		/// Возвращает LineNode, расположенная в позиции pos
		/// </summary>
		/// <param name="pos">Позиция должна быть там, где расположен LineNode</param>
		/// <returns>LineNode с positionRect.Contains(pos) или null в случае таких не существует</returns>
		public LineNode GetLineNodeAt(Vector2 pos)
			{
			foreach (LineNode ln in nodes)
				{
				if (ln.isVisible && ln.positionRect.Contains(pos))
					{
					return ln;
					}
				}
			return null;
			}

		/// <summary>
		/// Возвращает LineNodes, расположенные внутри прямоугольника r
		/// </summary>
		/// <param name="r">Поисковый прямоугольник</param>
        /// <returns>Список LineNodes, находящиеся в пределах r</returns>
		public List<LineNode> GetLineNodesAt(Rectangle r)
			{
			List<LineNode> toReturn = new List<LineNode>();
			foreach (LineNode ln in nodes)
				{
				if (ln.isVisible && r.Contains(ln.position))
					{
					toReturn.Add(ln);
					}
				}
			return toReturn;
			}


		private bool _nodeBoundsValid = false;
		private RectangleF _nodeBounds;

		/// <summary>
        /// Возвращает границы всех обрабатываемых LineNodes.
		/// </summary>
		/// <returns></returns>
		public RectangleF GetLineNodeBounds()
			{
			if (!_nodeBoundsValid)
				{
				if (_nodes.Count > 0)
					{
					double minX, maxX, minY, maxY;
					minX = minY = Double.PositiveInfinity;
					maxX = maxY = Double.NegativeInfinity;
					foreach (LineNode ln in _nodes)
						{
						minX = Math.Min(minX, Math.Min(ln.position.X, Math.Min(ln.inSlopeAbs.X, ln.outSlopeAbs.X)));
						maxX = Math.Max(maxX, Math.Max(ln.position.X, Math.Max(ln.inSlopeAbs.X, ln.outSlopeAbs.X)));
						minY = Math.Min(minY, Math.Min(ln.position.Y, Math.Min(ln.inSlopeAbs.Y, ln.outSlopeAbs.Y)));
						maxY = Math.Max(maxY, Math.Max(ln.position.Y, Math.Max(ln.inSlopeAbs.Y, ln.outSlopeAbs.Y)));
						}
					_nodeBounds = new RectangleF((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
					}
				else
					{
					_nodeBounds = new RectangleF();
					}
				_nodeBoundsValid = true;
				}
			return _nodeBounds;
			}

		/// <summary>
		/// 
		/// </summary>
		public void InvalidateNodeBounds()
			{
                // Список дел: заменить чистым узлом обработки/шаблон событий
			_nodeBoundsValid = false;
			}

		#endregion

		#region Methoden für Intersections

		/// <summary>
		/// Уничтожает Intersection и повсюду сообщает
		/// </summary>
		/// <param name="i">к уничтоженным Intersection</param>
		private void DestroyIntersection(Intersection i)
			{
			if (i != null)
				{
				i.Dispose();
				intersections.Remove(i);
				}
			}

		/// <summary>
		/// проверяет, действительно ли пересикаются aRect и bRect
		/// </summary>
		/// <param name="aRect">первый прямоугольник</param>
		/// <param name="bRect">второй прямоугольник</param>
		/// <returns></returns>
		private bool IntersectsTrue(RectangleF aRect, RectangleF bRect)
			{
			Interval<float> aHorizontal = new Interval<float>(aRect.Left, aRect.Right);
			Interval<float> aVertical = new Interval<float>(aRect.Top, aRect.Bottom);

			Interval<float> bHorizontal = new Interval<float>(bRect.Left, bRect.Right);
			Interval<float> bVertical = new Interval<float>(bRect.Top, bRect.Bottom);


			return (aHorizontal.IntersectsTrue(bHorizontal) && aVertical.IntersectsTrue(bVertical));
			}


		/// <summary>
		/// объединяет два списка вместе с парами пересечений и устраняет при этом найденные двойные пересечения.
		/// </summary>
		/// <param name="correctList">Список с найденными и перепроверенными пересечениями (не должно содержать двойных пересечений)</param>
        /// <param name="newList">Список с пересечениями, которые будут проверяться и при необходимости включены</param>
		/// <param name="aSegment">LineSegment левой части пары</param>
		/// <param name="bSegment">LineSegment правой части пары</param>
        /// <param name="tolerance">Как далеко должно быть до пар пересечений, что они могут быть признаны двойными</param>
		/// <returns>currectList, где все пересечения вставлены из newList, который находится по меньшей мере tolerance от любого другого пересечения.</returns>
		private List<Pair<double>> MergeIntersectionPairs(List<Pair<double>> correctList, List<Pair<double>> newList, LineSegment aSegment, LineSegment bSegment, double tolerance)
			{
			List<Pair<double>> toReturn = correctList;

			// смотреть каждую пару в newList
			foreach (Pair<double> p in newList)
				{
				// установить позицию Intersection...
				Vector2 positionOfP = aSegment.AtTime(p.Left);
				bool doInsert = true;

				// ...сравненить позиции с каждым Intersection в correctList...
				for (int i = 0; doInsert && i < correctList.Count; i++)
					{
					Vector2 foo = aSegment.AtTime(toReturn[i].Left);
					if ((foo - positionOfP).Abs <= 8*tolerance)
						{
                            // Мы нашли двойные пересечения, тогда переместим точку пересечения в середену
						doInsert = false;

						toReturn[i] = new Pair<double>(toReturn[i].Left + (p.Left - toReturn[i].Left) / 2, toReturn[i].Right + (p.Right - toReturn[i].Right) / 2);
						}
					}

				// ...и при случае вставить
				if (doInsert) 
					toReturn.Add(p);
				}

			return toReturn;
			}


		/// <summary>
		/// Находить все отрезке Findet alle Schnittpunkte zwischen aSegment und bSegment und gibt diese als Liste von Zeitpaaren zurück bei einer Genauigkeit von tolerance
		/// </summary>
		/// <param name="aSegment">первый LineSegment</param>
		/// <param name="bSegment">второй LineSegment</param>
		/// <param name="aTimeStart"></param>
		/// <param name="aTimeEnd"></param>
		/// <param name="bTimeStart"></param>
		/// <param name="bTimeEnd"></param>
        /// <param name="tolerance">Точность обзора: минимальная длина края проверяемого BoundingBox</param>
		/// <param name="aOriginalSegment">оригинальный LineSegment A, прежде чем он был разделен</param>
        /// <param name="bOriginalSegment">оригинальный LineSegment B, прежде чем он был разделен</param>
		/// <returns>Список пар, где находится одно пересечение: левая часть временных параметров первой кривая, правая часть временных парамтров второй кривая</returns>
		private List<Pair<double>> CalculateIntersections(
			LineSegment aSegment, LineSegment bSegment,
			double aTimeStart, double aTimeEnd,
			double bTimeStart, double bTimeEnd,
			double tolerance,
			LineSegment aOriginalSegment, LineSegment bOriginalSegment)
			{
			List<Pair<double>> foundIntersections = new List<Pair<double>>();

			// проверить рекурсивно но пересечении BoundingBoxen:
			// Список дел: возможно эфективнее чем рекурсивно? 

			RectangleF aBounds = aSegment.boundingRectangle;// .GetBounds(0);
			RectangleF bBounds = bSegment.boundingRectangle;// .GetBounds(0);

			// пересекаются BoundingBoxen? тогда стоит детально изучить
			if (IntersectsTrue(aBounds, bBounds)) // aBounds.IntersectsWith(bBounds)) //IntersectsTrue(aBounds, bBounds)) // 
				{
				// обе BoundingBoxen еще меньше чем tolerance, то мы нашли точку пересечения
				if ((aBounds.Width <= tolerance) && (aBounds.Height <= tolerance)
						&& (bBounds.Width <= tolerance) && (bBounds.Height <= tolerance))
					{
					foundIntersections.Add(new Pair<double>(aTimeStart + ((aTimeEnd - aTimeStart) / 2), bTimeStart + ((bTimeEnd - bTimeStart) / 2)));
					}

				// BoundingBox A уже достаточно мал, но BoundingBox B все ещё необходимо детально изучить:
				else if ((aBounds.Width <= tolerance) && (aBounds.Height <= tolerance))
					{
					double bTimeMiddle = bTimeStart + ((bTimeEnd - bTimeStart) / 2);

					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment, bSegment.subdividedFirst,
							aTimeStart, aTimeEnd,
							bTimeStart, bTimeMiddle,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2*tolerance);

					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment, bSegment.subdividedSecond,
							aTimeStart, aTimeEnd,
							bTimeMiddle, bTimeEnd,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2*tolerance);
					}

                // BoundingBox B еще достаточно мал, но BoundingBox A все ещё необходимо детально изучить:
				else if ((bBounds.Width <= tolerance) && (bBounds.Height <= tolerance))
					{
					double aTimeMiddle = aTimeStart + ((aTimeEnd - aTimeStart) / 2);

					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment.subdividedFirst, bSegment,
							aTimeStart, aTimeMiddle,
							bTimeStart, bTimeEnd,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2 * tolerance);
					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment.subdividedSecond, bSegment,
							aTimeMiddle, aTimeEnd,
							bTimeStart, bTimeEnd,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2 * tolerance);
					}

				// BoundingBoxen слишком большие - разделить линии  и исследовать части 2x2 на перекресткеe
				else
					{
					double aTimeMiddle = aTimeStart + ((aTimeEnd - aTimeStart) / 2);
					double bTimeMiddle = bTimeStart + ((bTimeEnd - bTimeStart) / 2);

					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment.subdividedFirst, bSegment.subdividedFirst,
							aTimeStart, aTimeMiddle,
							bTimeStart, bTimeMiddle,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2 * tolerance);
					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment.subdividedSecond, bSegment.subdividedFirst,
							aTimeMiddle, aTimeEnd,
							bTimeStart, bTimeMiddle,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2 * tolerance);

					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment.subdividedFirst, bSegment.subdividedSecond,
							aTimeStart, aTimeMiddle,
							bTimeMiddle, bTimeEnd,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2 * tolerance);
					foundIntersections = MergeIntersectionPairs(foundIntersections, CalculateIntersections(
							aSegment.subdividedSecond, bSegment.subdividedSecond,
							aTimeMiddle, aTimeEnd,
							bTimeMiddle, bTimeEnd,
							tolerance,
							aOriginalSegment, bOriginalSegment), aOriginalSegment, bOriginalSegment, 2 * tolerance);

					}
				}

			// Список дел: двойные перекрестки отфильтровать
            // Теперь мы отфильтровывать все перекрестки, которые были обнаружены дважды:
			/*
			for (int i = 0; i < foundIntersections.Count-1; i++)
				{
				for (int j = i+1; j < foundIntersections.Count; j++)
					{
					}
				}
			*/
			return foundIntersections;
			}

		#endregion

		#region Methoden für NodeConnections

		/// <summary>
		/// Berechnet alle Spurwechselstellen der NodeConnection nc mit anderen NodeConnections
		/// </summary>
		/// <param name="nc">zu untersuchende NodeConnection</param>
		/// <param name="distanceBetweenChangePoints">Distanz zwischen einzelnen LineChangePoints (quasi Genauigkeit)</param>
		/// <param name="maxDistanceToOtherNodeConnection">maximale Entfernung zwischen zwei NodeConnections zwischen denen ein Spurwechsel stattfinden darf</param>
		public void FindLineChangePoints(NodeConnection nc, double distanceBetweenChangePoints, double maxDistanceToOtherNodeConnection)
			{
			nc.ClearLineChangePoints();

			double currentArcPosition = distanceBetweenChangePoints/2;
			double delta = distanceBetweenChangePoints / 4;

			/*
			 * TODO: Spurwechsel funktioniert soweit so gut. Einziges Manko ist der Spurwechsel über LineNodes hinweg:
			 * 
			 * Zum einen, können so rote Ampeln überfahren werden und zum anderen, kommt es z.T. zu sehr komischen Situationen, wo
			 * das spurwechselnde Auto irgendwie denkt es sei viel weiter vorne und so mittendrin wartet und erst weiterfährt, wenn
			 * das Auto 20m weiter weg ist.
			 * 
			 * Als workaround werden jetzt doch erstmal Spurwechsel kurz vor LineNodes verboten, auch wenn das eigentlich ja gerade
			 * auch ein Ziel der hübschen Spurwechsel war.
			 * 
			 */

			// nur so lange suchen, wie die NodeConnection lang ist
			while (currentArcPosition < nc.lineSegment.length - distanceBetweenChangePoints/2)
				{
				Vector2 startl = nc.lineSegment.AtPosition(currentArcPosition - delta);
				Vector2 startr = nc.lineSegment.AtPosition(currentArcPosition + delta);

				Vector2 leftVector = nc.lineSegment.DerivateAtTime(nc.lineSegment.PosToTime(currentArcPosition - delta)).RotatedClockwise.Normalized;
				Vector2 rightVector = nc.lineSegment.DerivateAtTime(nc.lineSegment.PosToTime(currentArcPosition + delta)).RotatedCounterClockwise.Normalized;

				// Faule Implementierung:	statt Schnittpunkt Gerade/Bezierkurve zu berechnen nutzen wir vorhandenen
				//							Code und Berechnen den Schnittpunkt zwischen zwei Bezierkurven.
				// TODO:	Sollte das hier zu langsam sein, muss eben neuer optimierter Code her für die Berechnung
				//			von Schnittpunkten Gerade/Bezierkurve
				LineSegment leftLS = new LineSegment(0, startl, startl + 0.25 * maxDistanceToOtherNodeConnection * leftVector, startl + 0.75 * maxDistanceToOtherNodeConnection * leftVector, startl + maxDistanceToOtherNodeConnection * leftVector);
				LineSegment rightLS = new LineSegment(0, startr, startr + 0.25 * maxDistanceToOtherNodeConnection * rightVector, startr + 0.75 * maxDistanceToOtherNodeConnection * rightVector, startr + maxDistanceToOtherNodeConnection * rightVector);

				foreach (NodeConnection nc2 in _connections)
					{
					if (nc2.enableIncomingLineChange && (nc2.carsAllowed || nc2.busAllowed) && nc != nc2 && nc.startNode.networkLayer == nc2.startNode.networkLayer && nc.endNode.networkLayer == nc2.endNode.networkLayer)
						{
						// LINKS: Zeitparameterpaare ermitteln 
						List<Pair<double>> intersectionTimes = CalculateIntersections(leftLS, nc2.lineSegment, 0d, 1d, 0d, 1d, 8, leftLS, nc2.lineSegment);
						if (intersectionTimes != null)
							{
							// Startposition
							NodeConnection.SpecificPosition start = new NodeConnection.SpecificPosition(currentArcPosition - delta, nc);

							// LineChangePoints erstellen
							foreach (Pair<double> p in intersectionTimes)
								{
								// Winkel überprüfen
								if (Vector2.AngleBetween(nc.lineSegment.DerivateAtTime(nc.lineSegment.PosToTime(currentArcPosition - delta)), nc2.lineSegment.DerivateAtTime(p.Right)) < Constants.maximumAngleBetweenConnectionsForLineChangePoint)
									{
									NodeConnection.SpecificPosition otherStart = new NodeConnection.SpecificPosition(nc2, p.Right);

									// Einfädelpunkt des Fahrzeugs bestimmen und evtl. auf nächste NodeConnection weiterverfolgen:
									double distance = (nc.lineSegment.AtPosition(currentArcPosition - delta) - nc2.lineSegment.AtTime(p.Right)).Abs;

									// Einfädelpunkt:
									double arcPositionTarget = nc2.lineSegment.TimeToArcPosition(p.Right) + 3 * distance;

									if (arcPositionTarget <= nc2.lineSegment.length)
										{
										NodeConnection.SpecificPosition target = new NodeConnection.SpecificPosition(arcPositionTarget, nc2);
										nc.AddLineChangePoint(new NodeConnection.LineChangePoint(start, target, otherStart));
										}
									else
										{
										double diff = arcPositionTarget - nc2.lineSegment.length;
										foreach (NodeConnection nextNc in nc2.endNode.nextConnections)
											{
											if (   (diff <= nextNc.lineSegment.length)
												&& (nextNc.enableIncomingLineChange && (nextNc.carsAllowed || nextNc.busAllowed))
												&& (nc != nextNc))
												{
												NodeConnection.SpecificPosition target = new NodeConnection.SpecificPosition(diff, nextNc);
												nc.AddLineChangePoint(new NodeConnection.LineChangePoint(start, target, otherStart));
												}
											}
										}

									break;
									}
								}
							}

						// RECHTS: Zeitparameterpaare ermitteln
						intersectionTimes = CalculateIntersections(rightLS, nc2.lineSegment, 0d, 1d, 0d, 1d, 8, leftLS, nc2.lineSegment);
						if (intersectionTimes != null)
							{
							// Startposition
							NodeConnection.SpecificPosition start = new NodeConnection.SpecificPosition(currentArcPosition + delta, nc);

							// LineChangePoints erstellen
							foreach (Pair<double> p in intersectionTimes)
								{
								// Winkel überprüfen
								if (Vector2.AngleBetween(nc.lineSegment.DerivateAtTime(nc.lineSegment.PosToTime(currentArcPosition + delta)), nc2.lineSegment.DerivateAtTime(p.Right)) < Constants.maximumAngleBetweenConnectionsForLineChangePoint)
									{
									NodeConnection.SpecificPosition otherStart = new NodeConnection.SpecificPosition(nc2, p.Right);

									// Einfädelpunkt des Fahrzeugs bestimmen und evtl. auf nächste NodeConnection weiterverfolgen:
									double distance = (nc.lineSegment.AtPosition(currentArcPosition + delta) - nc2.lineSegment.AtTime(p.Right)).Abs;

									// Einfädelpunkt:
									double arcPositionTarget = nc2.lineSegment.TimeToArcPosition(p.Right) + 3 * distance;

									if (arcPositionTarget <= nc2.lineSegment.length)
										{
										NodeConnection.SpecificPosition target = new NodeConnection.SpecificPosition(arcPositionTarget, nc2);
										nc.AddLineChangePoint(new NodeConnection.LineChangePoint(start, target, otherStart));
										}
									else
										{
										double diff = arcPositionTarget - nc2.lineSegment.length;
										foreach (NodeConnection nextNc in nc2.endNode.nextConnections)
											{
											if ((diff <= nextNc.lineSegment.length)
												&& (nextNc.enableIncomingLineChange && (nextNc.carsAllowed || nextNc.busAllowed))
												&& (nc != nextNc))
												{
												NodeConnection.SpecificPosition target = new NodeConnection.SpecificPosition(diff, nextNc);
												nc.AddLineChangePoint(new NodeConnection.LineChangePoint(start, target, otherStart));
												}
											}
										}

									break;
									}
								}
							}
						}
					}

				currentArcPosition += distanceBetweenChangePoints;
				}
			}


		/// <summary>
		/// Entfernt alle LineChangePoints, die von nc ausgehen und evtl. eingehen
		/// </summary>
		/// <param name="nc">NodeConnection dessen ausgehende LineChangePoints gelöscht werden</param>
		/// <param name="removeOutgoingLineChangePoints">ausgehende LineChangePoints löschen</param>
		/// <param name="removeIncomingLineChangePoints">eingehende LineChangePoints löschen</param>
		public void RemoveLineChangePoints(NodeConnection nc, bool removeOutgoingLineChangePoints, bool removeIncomingLineChangePoints)
			{
			if (removeOutgoingLineChangePoints)
				{
				nc.ClearLineChangePoints();
				}
			
			if (removeIncomingLineChangePoints)
				{
				foreach (NodeConnection otherNc in _connections)
					{
					otherNc.RemoveAllLineChangePointsTo(nc);
					}
				}
			}

		/// <summary>
		/// Gibt eine Liste mit allen Schnittpunkten von nc mit anderen existierenden NodeConnections
		/// </summary>
		/// <param name="nc">zu untersuchende NodeConnection</param>
		/// <param name="tolerance">Toleranz (maximale Kantenlänge der Boundingboxen)</param>
		/// <returns>Eine Liste mit Intersection Objekten die leer ist, falls keine Schnittpunkte existieren</returns>
		private List<Intersection> CalculateIntersections(NodeConnection nc, double tolerance)
			{
			List<Intersection> toReturn = new List<Intersection>();

			foreach (NodeConnection nc2 in connections)
				{
				if (nc != nc2)
					{
					if (   (nc.startNode.networkLayer == nc.endNode.networkLayer && (nc2.startNode.networkLayer == nc.startNode.networkLayer || nc2.endNode.networkLayer == nc.endNode.networkLayer))
						|| (nc.startNode.networkLayer != nc.endNode.networkLayer && (   nc2.startNode.networkLayer == nc.startNode.networkLayer
																					 || nc2.startNode.networkLayer == nc.endNode.networkLayer
																					 || nc2.endNode.networkLayer == nc.startNode.networkLayer 
																					 || nc2.endNode.networkLayer == nc.endNode.networkLayer)))
						{
						// Zeitparameterpaare ermitteln
						List<Pair<double>> intersectionTimes = CalculateIntersections(nc.lineSegment, nc2.lineSegment, 0d, 1d, 0d, 1d, tolerance, nc.lineSegment, nc2.lineSegment);
						if (intersectionTimes != null)
							{
							// Intersections erstellen
							foreach (Pair<double> p in intersectionTimes)
								{
								toReturn.Add(new Intersection(nc, nc2, p.Left, p.Right));
								}
							}
						}
					}
				}

			return toReturn;
			}

		/// <summary>
		/// Berechnet alle Schnittpunkte von nc mit anderen existierenden NodeConnections und meldet sie an
		/// </summary>
		/// <param name="nc">zu untersuchende NodeConnection</param>
		public void FindIntersections(NodeConnection nc)
			{
			// erstmal bestehende Intersections dieser NodeConnections zerstören
			for (int i = 0; i < intersections.Count; i++)
				{
				if ((intersections[i]._aConnection == nc) || (intersections[i]._bConnection == nc))
					{
					intersections[i].Dispose();
					intersections.RemoveAt(i);
					i--;
					}
				}

			// jetzt können wir nach neuen Intersections suchen und diese anmelden
			List<Intersection> foundIntersections = CalculateIntersections(nc, 0.25d);
			foreach (Intersection i in foundIntersections)
				{
				i._aConnection.AddIntersection(i);
				i._bConnection.AddIntersection(i);
				intersections.Add(i);
				}
			}

		/// <summary>
		/// Gibt die erstbeste NodeConnection von from nach to zurück
		/// (es sollte eigentlich immer nur eine existieren, das wird aber nicht weiter geprüft)
		/// </summary>
		/// <param name="from">LineNode von dem die NodeConnection ausgeht</param>
		/// <param name="to">LineNode zu der die NodeConnection hingehet</param>
		/// <returns>NodeConnection, welche von from nach to läuft oder null, falls keine solche existiert</returns>
		public NodeConnection GetNodeConnection(LineNode from, LineNode to)
			{
			foreach (NodeConnection nc in this.connections)
				{
				if ((nc.startNode == from) && (nc.endNode == to))
					{
					return nc;
					}
				}
			return null;
			}

		/// <summary>
		/// Aktualisiert die NodeConnection ncToUpdate
		/// (Bezierkurve neu berechnen, etc.)
		/// </summary>
		/// <param name="ncToUpdate">zu aktualisierende NodeConnection</param>
		public void UpdateNodeConnection(NodeConnection ncToUpdate)
			{
			ncToUpdate.lineSegment = null;
			ncToUpdate.lineSegment = new LineSegment(0, ncToUpdate.startNode.position, ncToUpdate.startNode.outSlopeAbs, ncToUpdate.endNode.inSlopeAbs, ncToUpdate.endNode.position);
			FindIntersections(ncToUpdate);


			if (ncToUpdate.enableIncomingLineChange)
				{
				// TODO:	diese Lösung ist viel zu unperformant! Da muss was anderes her.

				/*
				RemoveLineChangePoints(ncToUpdate, false, true);
				foreach (NodeConnection nc in m_connections)
					{
					if (nc.enableOutgoingLineChange)
						{
						FindLineChangePoints(nc, Constants.maxDistanceToLineChangePoint, Constants.maxDistanceToParallelConnection);
						}
					}*/
				}
			else
				{
				// TODO: überlegen, ob hier wirklich nichts gemacht werden muss
				}

			if (ncToUpdate.enableOutgoingLineChange && (ncToUpdate.carsAllowed || ncToUpdate.busAllowed))
				{
				RemoveLineChangePoints(ncToUpdate,true, false);
				FindLineChangePoints(ncToUpdate, Constants.maxDistanceToLineChangePoint, Constants.maxDistanceToParallelConnection);
				}
			else
				{
				// TODO: überlegen, ob hier wirklich nichts gemacht werden muss
				}

			InvalidateNodeBounds();
			}

		/// <summary>
		/// Teilt die NodeConnection nc in zwei einzelne NodeConnections auf.
		/// Dabei wird in der Mitte natürlich auch ein neuer LineNode erstellt
		/// </summary>
		/// <param name="nc">aufzuteilende NodeConnection</param>
		public void SplitNodeConnection(NodeConnection nc)
			{
			LineNode startNode = nc.startNode;
			LineNode endNode = nc.endNode;

			// Mittelknoten erstellen
			LineNode middleNode = new LineNode(nc.lineSegment.subdividedFirst.p3, nc.startNode.networkLayer, false);
			middleNode.inSlopeAbs = nc.lineSegment.subdividedFirst.p2;
			middleNode.outSlopeAbs = nc.lineSegment.subdividedSecond.p1;
			nodes.Add(middleNode);

			// Anfangs- und Endknoten bearbeiten
			startNode.outSlopeAbs = nc.lineSegment.subdividedFirst.p1;
			endNode.inSlopeAbs = nc.lineSegment.subdividedSecond.p2;

			// Alte Connections lösen
			Disconnect(startNode, endNode);

			// Neue Connections bauen
			Connect(startNode, middleNode, nc.priority, nc.targetVelocity, nc.carsAllowed, nc.busAllowed, nc.tramAllowed, nc.enableIncomingLineChange, nc.enableOutgoingLineChange);
			Connect(middleNode, endNode, nc.priority, nc.targetVelocity, nc.carsAllowed, nc.busAllowed, nc.tramAllowed, nc.enableIncomingLineChange, nc.enableOutgoingLineChange);
			}


		/// <summary>
		/// gibt die NodeConnection zuück, welche sich bei position befindet
		/// </summary>
		/// <param name="position">Position, wo gesucht werden soll</param>
		/// <returns>erstbeste NodeConnection, welche durch den Punkt position läuft oder null, falls keine solche existiert</returns>
		public NodeConnection GetNodeConnectionAt(Vector2 position)
			{
			foreach (NodeConnection nc in connections)
				{
				if (nc.lineSegment.Contains(position))
					{
					return nc;
					}
				}
			return null;
			}


		/// <summary>
		/// liefert das IVehicle an den Weltkoordinaten position zurück.
		/// Momentan wird dabei nur ein Bereich von 30x30Pixeln um die Front des Fahrzeuges herum überprüft)
		/// </summary>
		/// <param name="position">Weltkoordinatenposition, wo nach einem Fahrzeug gesucht werden soll</param>
		/// <returns>Ein IVehicle dessen Front im Umkreis von 15 Pixeln um position herum ist.</returns>
		public IVehicle GetVehicleAt(Vector2 position)
			{
			foreach (NodeConnection nc in connections)
				{
				if (nc.lineSegment.Contains(position, 5, 15))
					{
					foreach (IVehicle v in nc.vehicles)
						{
						if (v.state.boundingRectangle.Contains(position))
							{
							return v;
							}
						}
					}
				}
			return null;
			}

		/// <summary>
		/// Resets the average velocities array of each NodeConnection.
		/// </summary>
		public void ResetAverageVelocities()
			{
			int numBuckets = (int)(GlobalTime.Instance.cycleTime * GlobalTime.Instance.ticksPerSecond);
			foreach (NodeConnection nc in _connections)
				{
				nc.ResetStatistics(numBuckets);
				}
			}

		/// <summary>
		/// Resets the average velocities array of the given NodeConnection.
		/// </summary>
		/// <param name="nc">The NodeConection to reset</param>
		public void ResetAverageVelocities(NodeConnection nc)
			{
			int numBuckets = (int)(GlobalTime.Instance.cycleTime * GlobalTime.Instance.ticksPerSecond);
			nc.ResetStatistics(numBuckets);
			}


		#endregion

		#region Statistiken

		/// <summary>
        /// включена визуализация статистики в NodeConnections
		/// </summary>
		/// <param name="state">Статус визуализации который необхоимо установить</param>
		public void setVisualizationInNodeConnections(bool state)
			{
			foreach (NodeConnection nc in _connections)
				{
				nc.visualizeAverageSpeed = state;
				}
			}


		#endregion

		#region Network rendering methods

		/// <summary>
		/// Network rendering options
		/// </summary>
		[Serializable]
		public class RenderOptions
			{
			/// <summary>
			/// Render LineNodes
			/// </summary>
			public bool renderLineNodes = true;

			/// <summary>
			/// Render NodeConnections
			/// </summary>
			public bool renderNodeConnections = true;

			/// <summary>
			/// Render Vehicles
			/// </summary>
			public bool renderVehicles = true;

			/// <summary>
			/// Perform clipping
			/// </summary>
			public bool performClipping = false;

			/// <summary>
			/// Clipping range in world coordinates
			/// </summary>
			public Rectangle clippingRect = new Rectangle();

			/// <summary>
			/// Render intersections
			/// </summary>
			public bool renderIntersections = false;

			/// <summary>
			/// Render LineChangePoints
			/// </summary>
			public bool renderLineChangePoints = false;

			/// <summary>
			/// Render debug data of LineNodes
			/// </summary>
			public bool renderLineNodeDebugData = false;

			/// <summary>
			/// Render debug data of NodeConnections
			/// </summary>
			public bool renderNodeConnectionDebugData = false;

			/// <summary>
			/// Render debug data of Vehicles
			/// </summary>
			public bool renderVehicleDebugData = false;

			/// <summary>
			/// Perform mapping of vehicle's acceleration and velocity to color
			/// </summary>
			public bool vehicleVelocityMapping = false;
			}

		/// <summary>
		/// Performs the network rendering on given graphics canvas.
		/// Controller classes may be null if corresponding flags in options are set to false.
		/// </summary>
		/// <param name="g">Render canvas</param>
		/// <param name="options">Network rendering options</param>
		public void RenderNetwork(Graphics g, RenderOptions options)
			{
			if (options.renderNodeConnections)
				{
				foreach (NodeConnection nc in connections)
					{
					if ((nc.startNode.isVisible || nc.endNode.isVisible) && (!options.performClipping || nc.lineSegment.boundingRectangle.IntersectsWith(options.clippingRect)))
						nc.Draw(g);
					}
				}

			foreach (LineNode ln in nodes)
				{
				if (ln.tLight != null || ln.stopSign || options.renderLineNodes)
					{
					if (ln.isVisible && (!options.performClipping || ln.positionRect.IntersectsWith(options.clippingRect)))
						{
						ln.Draw(g);
						}						
					}
				}

			if (options.renderVehicles)
				{
				foreach (NodeConnection nc in connections)
					{
					if ((nc.startNode.isVisible || nc.endNode.isVisible) && (!options.performClipping || nc.lineSegment.boundingRectangle.IntersectsWith(options.clippingRect)))
						{
						foreach (IVehicle v in nc.vehicles)
							{
							v.Draw(g, options.vehicleVelocityMapping);
							}
						}
					}
				}

			if (options.renderIntersections)
				{
				using (Pen redPen = new Pen(Color.Red, 1.0f))
					{
					using (Pen yellowPen = new Pen(Color.Orange, 1.0f))
						{
						using (Pen greenPen = new Pen(Color.Green, 1.0f))
							{
							foreach (Intersection i in intersections)
								{
								if (i._aConnection.startNode.isVisible || i._aConnection.endNode.isVisible || i._bConnection.startNode.isVisible || i._bConnection.endNode.isVisible)
									{
									PointF[] surroundingPoints = new PointF[4]
											{
												i._aConnection.lineSegment.AtPosition(i.aArcPosition - i._frontWaitingDistance),
												i._bConnection.lineSegment.AtPosition(i.bArcPosition - i._frontWaitingDistance),
												i._aConnection.lineSegment.AtPosition(i.aArcPosition + i._rearWaitingDistance),
												i._bConnection.lineSegment.AtPosition(i.bArcPosition + i._rearWaitingDistance)
											};

									if (i.avoidBlocking)
										{
										g.DrawLine(redPen, i.aPosition, i.bPosition);
										g.DrawPolygon(redPen, surroundingPoints);
										}
									else if (i._aConnection.startNode != i._bConnection.startNode || (i._frontWaitingDistance < i.aArcPosition && i._frontWaitingDistance < i.bArcPosition))
										{
										g.DrawLine(yellowPen, i.aPosition, i.bPosition);
										g.DrawPolygon(yellowPen, surroundingPoints);
										}
									else
										{
										g.DrawLine(greenPen, i.aPosition, i.bPosition);
										g.DrawPolygon(greenPen, surroundingPoints);
										}
									}
								}
							}
						}
					}
				}

			if (options.renderLineChangePoints)
				{
				foreach (NodeConnection nc in connections)
					{
					if (!options.performClipping || nc.lineSegment.boundingRectangle.IntersectsWith(options.clippingRect))
						nc.DrawLineChangePoints(g);
					}
				}

			if (options.renderLineNodeDebugData)
				{
				foreach (LineNode ln in nodes)
					{
					if (!options.performClipping || ln.positionRect.IntersectsWith(options.clippingRect))
						ln.DrawDebugData(g);
					}
				}

			if (options.renderVehicleDebugData)
				{
				foreach (NodeConnection nc in connections)
					{
					if (!options.performClipping || nc.lineSegment.boundingRectangle.IntersectsWith(options.clippingRect))
						{
						using (Pen greenPen = new Pen(Color.Green, 3))
							{
							foreach (IVehicle v in nc.vehicles)
								{
								v.DrawDebugData(g);
								if (v.state.vehicleThatLetsMeChangeLine != null)
									{
									g.DrawLine(greenPen, v.state.positionAbs, v.state.vehicleThatLetsMeChangeLine.state.positionAbs);
									}
								}
							}
						}
					}
				}
			}

		#endregion

		#region NetworkLayer stuff

		/// <summary>
        /// Добавляет визуализации сетевого уровня с указанным заголовком.
		/// </summary>
        /// <param name="title">Заголовок новой визуализации сетевого уровня</param>
		/// <param name="visible">Видимый знак нового сетевого уровня</param>
		public void AddNetworkLayer(string title, bool visible)
			{
			NetworkLayer nl = new NetworkLayer(title, visible);
			nl.TitleChanged += new NetworkLayer.TitleChangedEventHandler(nl_TitleChanged);
			nl.VisibleChanged += new NetworkLayer.VisibleChangedEventHandler(nl_VisibleChanged);
			_networkLayers.Add(nl);
			InvokeNetworkLayersChanged(new NetworkLayersChangedEventArgs(NetworkLayersChangedEventArgs.InvalidationLevel.COLLECTION_CHANGED));
			}

		/// <summary>
		/// Пытается удалить данную NetworkLayer. Удается только, если <paramref name="nl"/> не назначен какой-либо управляемый LineNode.
		/// </summary>
		/// <param name="nl">NetworkLayer удалить</param>
		/// <returns>True, если nl успещно удален. False, если nl по прежнему назначен, по меньшей мере один LineNode in _nodes.</returns>
		public bool RemoveNetworkLayer(NetworkLayer nl)
			{
			if (_networkLayers.Count <= 1)
				return false;

			foreach (LineNode ln in _nodes)
				{
				if (ln.networkLayer == nl)
					return false;
				}

			_networkLayers.Remove(nl);
			nl.VisibleChanged -= nl_VisibleChanged;
			nl.TitleChanged -= nl_TitleChanged;
			InvokeNetworkLayersChanged(new NetworkLayersChangedEventArgs(NetworkLayersChangedEventArgs.InvalidationLevel.COLLECTION_CHANGED));
			return true;
			}

		void nl_VisibleChanged(object sender, NetworkLayer.VisibleChangedEventArgs e)
			{
			InvokeNetworkLayersChanged(new NetworkLayersChangedEventArgs(NetworkLayersChangedEventArgs.InvalidationLevel.ONLY_VISIBILITY_CHANGED));
			}

		void nl_TitleChanged(object sender, NetworkLayer.TitleChangedEventArgs e)
			{
			InvokeNetworkLayersChanged(new NetworkLayersChangedEventArgs(NetworkLayersChangedEventArgs.InvalidationLevel.ONLY_TITLES_CHANGED));
			}

		#region NetworkLayersChanged event

		/// <summary>
		/// EventArgs for a NetworkLayersChanged event
		/// </summary>
		public class NetworkLayersChangedEventArgs : EventArgs
			{
			/// <summary>
			/// Level of Invalidation during the NetworkLayersChangedEvent
			/// </summary>
			public enum InvalidationLevel
				{
				/// <summary>
				/// Only the visibility has changed
				/// </summary>
				ONLY_VISIBILITY_CHANGED,
				/// <summary>
				/// Only the tiltes have changed
				/// </summary>
				ONLY_TITLES_CHANGED,
				/// <summary>
				/// The whole collection has changed
				/// </summary>
				COLLECTION_CHANGED
				}

			/// <summary>
			/// Level of Invalidation during the NetworkLayersChangedEvent
			/// </summary>
			public NetworkLayersChangedEventArgs.InvalidationLevel _invalidationLevel;

			/// <summary>
			/// Creates new NetworkLayersChangedEventArgs
			/// </summary>
			/// <param name="il">Level of Invalidation during the NetworkLayersChangedEvent</param>
			public NetworkLayersChangedEventArgs(InvalidationLevel il)
				{
				_invalidationLevel = il;
				}
			}

		/// <summary>
		/// Delegate for the NetworkLayersChanged-EventHandler, which is called when the list of network layers has changed
		/// </summary>
		/// <param name="sender">Sneder of the event</param>
		/// <param name="e">Event parameter</param>
		public delegate void NetworkLayersChangedEventHandler(object sender, NetworkLayersChangedEventArgs e);

		/// <summary>
		/// The NetworkLayersChanged event occurs when the list of network layers has changed
		/// </summary>
		public event NetworkLayersChangedEventHandler NetworkLayersChanged;

		/// <summary>
		/// Helper method to initiate the NetworkLayersChanged event
		/// </summary>
		/// <param name="e">Event parameters</param>
		protected void InvokeNetworkLayersChanged(NetworkLayersChangedEventArgs e)
			{
			if (NetworkLayersChanged != null)
				{
				NetworkLayersChanged(this, e);
				}
			}

		#endregion

		#endregion

		#region Speichern/Laden

		/// <summary>
		/// Speichert alle verwalteten Daten in eine XML Datei
		/// </summary>
		/// <param name="xw">XMLWriter, in denen die verwalteten Daten gespeichert werden soll</param>
		/// <param name="xsn">zugehöriger XML-Namespace</param>
		public void SaveToFile(XmlWriter xw, XmlSerializerNamespaces xsn)
			{
			try
				{
				// Alles fürs Speichern vorbereiten
				foreach (NetworkLayer nl in _networkLayers)
					{
					nl._nodeHashes.Clear();
					}
				foreach (LineNode ln in nodes)
					{
					ln.PrepareForSave();
					if (ln.networkLayer != null)
						ln.networkLayer._nodeHashes.Add(ln.hashcode);
					}
				foreach (NodeConnection nc in connections)
					{
					nc.PrepareForSave();
					}
					
				// zunächst das Layout speichern
				xw.WriteStartElement("Layout");

					xw.WriteStartElement("title");
					xw.WriteString(m_title);
					xw.WriteEndElement();

					xw.WriteStartElement("infoText");
					xw.WriteString(_infoText);
					xw.WriteEndElement();

					// LineNodes serialisieren
					XmlSerializer xsLN = new XmlSerializer(typeof(LineNode));
					foreach (LineNode ln in nodes)
						{
						xsLN.Serialize(xw, ln, xsn);
						}

					// serialize NetworkLayers
					XmlSerializer xsNL = new XmlSerializer(typeof(NetworkLayer));
					foreach (NetworkLayer nl in _networkLayers)
						{
						xsNL.Serialize(xw, nl, xsn);
						}

					// NodeConnections serialisieren
					XmlSerializer xsNC = new XmlSerializer(typeof(NodeConnection));
					foreach (NodeConnection nc in connections)
						{
						xsNC.Serialize(xw, nc, xsn);
						}

					xw.WriteEndElement();
				}
			catch (IOException ex)
				{
				MessageBox.Show(ex.Message);
				throw;
				}
			}


		/// <summary>
		/// Läd eine XML Datei und versucht daraus den gespeicherten Zustand wiederherzustellen
		/// </summary>
		/// <param name="xd">XmlDocument mit den zu ladenden Daten</param>
		/// <param name="lf">LoadingForm für Statusinformationen</param>
		public List<ModelManager> LoadFromFile(XmlDocument xd, LoadingForm.LoadingForm lf)
			{
			lf.SetupLowerProgess("Parsing XML...", 3);

			List<ModelManager> toReturn = new List<ModelManager>();
			int saveVersion = 0;

			// erstma alles vorhandene löschen
			nodes.Clear();
			connections.Clear();
			intersections.Clear();
			_networkLayers.Clear();

			XmlNode mainNode = xd.SelectSingleNode("//CityTrafficSimulator");
			XmlNode saveVersionNode = mainNode.Attributes.GetNamedItem("saveVersion");
			if (saveVersionNode != null)
				saveVersion = Int32.Parse(saveVersionNode.Value);
			else
				saveVersion = 0;

			XmlNode titleNode = xd.SelectSingleNode("//CityTrafficSimulator/Layout/title");
			if (titleNode != null)
				{
				m_title = titleNode.InnerText;
				}

			XmlNode infoNode = xd.SelectSingleNode("//CityTrafficSimulator/Layout/infoText");
			if (infoNode != null)
				{
				_infoText = infoNode.InnerText;
				}

			lf.StepLowerProgress();

			// entsprechenden Node auswählen
			XmlNodeList xnlLineNode = xd.SelectNodes("//CityTrafficSimulator/Layout/LineNode");
			foreach (XmlNode aXmlNode in xnlLineNode)
				{
				// Node in einen TextReader packen
				TextReader tr = new StringReader(aXmlNode.OuterXml);
				// und Deserializen
				XmlSerializer xs = new XmlSerializer(typeof(LineNode));
				LineNode ln = (LineNode)xs.Deserialize(tr);

				// ab in die Liste
				nodes.Add(ln);
				}

			lf.StepLowerProgress();

			// entsprechenden Node auswählen
			XmlNodeList xnlNetworkLayer = xd.SelectNodes("//CityTrafficSimulator/Layout/NetworkLayer");
			foreach (XmlNode aXmlNode in xnlNetworkLayer)
				{
				// Node in einen TextReader packen
				TextReader tr = new StringReader(aXmlNode.OuterXml);
				// und Deserializen
				XmlSerializer xs = new XmlSerializer(typeof(NetworkLayer));
				NetworkLayer nl = (NetworkLayer)xs.Deserialize(tr);

				// ab in die Liste
				_networkLayers.Add(nl);
				}

			lf.StepLowerProgress();

			// entsprechenden NodeConnections auswählen
			XmlNodeList xnlNodeConnection = xd.SelectNodes("//CityTrafficSimulator/Layout/NodeConnection");
			foreach (XmlNode aXmlNode in xnlNodeConnection)
				{
				// Node in einen TextReader packen
				TextReader tr = new StringReader(aXmlNode.OuterXml);
				// und Deserializen
				XmlSerializer xs = new XmlSerializer(typeof(NodeConnection));
				NodeConnection ln = (NodeConnection)xs.Deserialize(tr);
				// ab in die Liste
				connections.Add(ln);
				}

			lf.SetupLowerProgess("Restoring LineNodes...", _nodes.Count);

			// Nodes wiederherstellen
			foreach (LineNode ln in _nodes)
				{
				ln.RecoverFromLoad(saveVersion, _nodes);
				lf.StepLowerProgress();
				}

			lf.SetupLowerProgess("Restoring NetworkLayers...", _networkLayers.Count);

			// restore NetworkLayers
			if (saveVersion >= 6)
				{
				foreach (NetworkLayer nl in _networkLayers)
					{
					foreach (int hash in nl._nodeHashes)
						{
						LineNode tmp = GetLineNodeByHash(nodes, hash);
						if (tmp != null)
							tmp.networkLayer = nl;
						}
					nl.TitleChanged += new NetworkLayer.TitleChangedEventHandler(nl_TitleChanged);
					nl.VisibleChanged +=new NetworkLayer.VisibleChangedEventHandler(nl_VisibleChanged);
					}
				}
			else
				{
				AddNetworkLayer("Layer 1", true);
				foreach (LineNode ln in _nodes)
					{
					ln.networkLayer = _networkLayers[0];
					}
				}
			InvokeNetworkLayersChanged(new NetworkLayersChangedEventArgs(NetworkLayersChangedEventArgs.InvalidationLevel.COLLECTION_CHANGED));

			lf.SetupLowerProgess("Restoring NodeConnections...", _connections.Count);

			// LineNodes wiederherstellen
			foreach (NodeConnection nc in _connections)
				{
				nc.RecoverFromLoad(saveVersion, nodes);
				TellNodesTheirConnection(nc);
				nc.lineSegment = new LineSegment(0, nc.startNode.position, nc.startNode.outSlopeAbs, nc.endNode.inSlopeAbs, nc.endNode.position);

				lf.StepLowerProgress();
				}

			lf.SetupLowerProgess("Calculate Intersections and Line Change Points...", _connections.Count);

			// Intersections wiederherstellen
			foreach (NodeConnection nc in _connections)
				{
				UpdateNodeConnection(nc);

				lf.StepLowerProgress();
				}


			// Fahraufträge laden
			// entsprechenden Node auswählen
			if (saveVersion < 5)
				{
				XmlNodeList xnlAuftrag = xd.SelectNodes("//CityTrafficSimulator/TrafficControl/Auftrag");

				lf.SetupLowerProgess("Load Old TrafficControl Volume...", 2 * xnlAuftrag.Count);

				foreach (XmlNode aXmlNode in xnlAuftrag)
					{
					// Node in einen TextReader packen
					TextReader tr = new StringReader(aXmlNode.OuterXml);
					// und Deserializen
					XmlSerializer xs = new XmlSerializer(typeof(ModelManager));
					ModelManager ln = (ModelManager)xs.Deserialize(tr);

					// in alten Dateien wurde das Feld häufigkeit statt trafficDensity gespeichert. Da es dieses Feld heute nicht mehr gibt, müssen wir konvertieren:
					if (saveVersion < 1)
						{
						// eigentlich wollte ich hier direkt mit aXmlNode arbeiten, das hat jedoch komische Fehler verursacht (SelectSingleNode) wählt immer den gleichen aus)
						// daher der Umweg über das neue XmlDocument.
						XmlDocument doc = new XmlDocument();
						XmlElement elem = doc.CreateElement("Auftrag");
						elem.InnerXml = aXmlNode.InnerXml;
						doc.AppendChild(elem);

						XmlNode haeufigkeitNode = doc.SelectSingleNode("//Auftrag/häufigkeit");
						if (haeufigkeitNode != null)
							{
							ln.trafficDensity = 72000 / Int32.Parse(haeufigkeitNode.InnerXml);
							}
						haeufigkeitNode = null;
						}

					// ab in die Liste
					toReturn.Add(ln);

					lf.StepLowerProgress();
					}

				// Nodes wiederherstellen
				foreach (ModelManager a in toReturn)
					{
					a.RecoverFromLoad(saveVersion, nodes);

					lf.StepLowerProgress();
					}

				}

			return toReturn;
			}

		/// <summary>
		/// Gibt den LineNode aus nodesList zurück, dessen Hash mit hash übereinstimmt
		/// </summary>
		/// <param name="nodesList">zu durchsuchende Liste von LineNodes</param>
		/// <param name="hash">auf Gleichheit zu überprüfender Hashcode</param>
		/// <returns>den erstbesten LineNode mit GetHashCode() == hash oder null, falls kein solcher existiert</returns>
		private LineNode GetLineNodeByHash(List<LineNode> nodesList, int hash)
			{
			foreach (LineNode ln in nodesList)
				{
				if (ln.GetHashCode() == hash)
					{
					return ln;
					}
				}
			return null;
			}

		#endregion
		
		#region ISavable Member

		/// <summary>
		/// подговтовить все необходимое для сохранения
		/// (генироровать хэши etc.)
		/// </summary>
		public void PrepareForSave()
			{
			foreach (LineNode ln in nodes)
				{
				ln.PrepareForSave();
				}
			foreach (NodeConnection nc in connections)
				{
				nc.PrepareForSave();
				}
			}

		/// <summary>
		/// Восстанавливает после успешной десериализации все внутренние ссылки снова
		/// </summary>
		/// <param name="saveVersion">Version сохраненных данных</param>
        /// <param name="nodesList">Список уже восстановленных LineNodes</param>
		public void RecoverFromLoad(int saveVersion, List<LineNode> nodesList)
			{
			throw new NotImplementedException();
			}

		#endregion

		#region ITickable Member

		/// <summary>
		/// говорить всем управляемым объектам связи, что они должны работать
		/// </summary>
		public void Tick(double tickLength)
			{
			foreach (LineNode ln in nodes)
				{
				ln.Tick(tickLength);
				}

			int bucketNumber = GlobalTime.Instance.currentCycleTick;
			foreach (NodeConnection nc in _connections)
				{
				nc.GatherStatistics(bucketNumber);
				}
			}

		/// <summary>
		/// сбрасывает состояние Tick всех LineNodes
		/// </summary>
		public void Reset()
			{
			foreach (LineNode ln in nodes)
				{
				ln.Reset();
				}
			}
		
		#endregion

		}
	}

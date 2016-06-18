using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Drawing;

using CityTrafficSimulator.Timeline;

namespace CityTrafficSimulator
    {
	/// <summary>
        /// Инкапсулирует светофор и осуществляет запись на TimelineEntry
	/// </summary>
    [Serializable]
    public class TrafficLight : TimelineEntry, ISavable
        {
		/// <summary>
		/// Статус светофора (красный или зеленый)
		/// </summary>
        public enum State {
			/// <summary>
			/// зеленый цвет светофора
			/// </summary>
            GREEN, 

			/// <summary>
            /// красный цвет светофора
			/// </summary>
			RED
            }


		/// <summary>
		/// текущее состояние светофора
		/// </summary>
		private State _trafficLightState;
		/// <summary>
        /// текущее состояние светофора
		/// </summary>
		[XmlIgnore]
        public State trafficLightState
            {
            get { return _trafficLightState; }
			set { _trafficLightState = value; }
            }

		/// <summary>
		/// Список LineNodes к которым данный TrafficLight относится
		/// </summary>
		[XmlIgnore]
		private List<LineNode> _assignedNodes = new List<LineNode>();

		/// <summary>
        /// Список LineNodes к которым данный TrafficLight относится
		/// </summary>
		[XmlIgnore]
		public List<LineNode> assignedNodes
			{
			get { return _assignedNodes; }
			}

		#region Hashcodes

		/*
		 * Nachdem der ursprüngliche Ansatz zu Hashen zu argen Kollisionen geführt hat, nun eine verlässliche Methode für Kollisionsfreie Hashes 
		 * mittels eindeutiger IDs für jedes TrafficLight die über statisch Klassenvariablen vergeben werden
		 */

		/// <summary>
		/// Klassenvariable welche den letzten vergebenen hashcode speichert und bei jeder Instanziierung eines Objektes inkrementiert werden muss
		/// </summary>
		[XmlIgnore]
		private static int hashcodeIndex = 0;

		/// <summary>
		/// Hashcode des instanziierten Objektes
		/// </summary>
		public int hashcode = -1;

		/// <summary>
		/// gibt den Hashcode des Fahrzeuges zurück.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
			{
			return hashcode;
			}

		/// <summary>
		/// Setzt die statische Klassenvariable hashcodeIndex zurück. Achtung: darf nur in bestimmten Fällen aufgerufen werden.
		/// </summary>
		public static void ResetHashcodeIndex()
			{
			hashcodeIndex = 0;
			}

		#endregion
		
        #region Konstruktoren

		/// <summary>
		/// Konstruktor für TimelineEntry-Ampeln
		/// </summary>
		public TrafficLight()
			{
			hashcode = hashcodeIndex++;

			// Initial Event anlegen
			this.defaultAction = SwitchToRed;
			trafficLightState = State.RED;
			this.color = Color.Red;
			}
        #endregion


        #region Speichern/Laden
		/// <summary>
		/// DEPRECATED: Hash des Elternknotens (wird für Serialisierung gebraucht)
		/// </summary>
		[XmlIgnore]
		public int parentNodeHash = 0;

		/// <summary>
		/// Hashes der zugeordneten LineNodes
		/// </summary>
		public List<int> assignedNodesHashes = new List<int>();

		/// <summary>
		/// bereitet das TrafficLight auf die XML-Serialisierung vor.
		/// </summary>
        public override void PrepareForSave()
            {
			base.PrepareForSave();

			assignedNodesHashes.Clear();
			foreach (LineNode ln in _assignedNodes)
				{
				assignedNodesHashes.Add(ln.GetHashCode());
				}
            }
		/// <summary>
		/// stellt das TrafficLight nach einer XML-Deserialisierung wieder her
		/// </summary>
		/// <param name="saveVersion">Version der gespeicherten Datei</param>
		/// <param name="nodesList">Liste von allen existierenden LineNodes</param>
        public override void RecoverFromLoad(int saveVersion, List<LineNode> nodesList)
            {
			// Klassenvariable für Hashcode erhöhen um Kollisionen für zukünftige LineNodes zu verhindern
			if (hashcodeIndex <= hashcode)
				{
				hashcodeIndex = hashcode + 1;
				}

			// erstmal EventActions setzen
			this.defaultAction = SwitchToRed;
			foreach (TimelineEvent e in events)
				{
				e.eventStartAction = SwitchToGreen;
				e.eventEndAction = SwitchToRed;
				}

			// nun die assignedNodes aus der nodesList dereferenzieren
			foreach (int hash in assignedNodesHashes)
				{
				foreach (LineNode ln in nodesList)
					{
					if (ln.GetHashCode() == hash)
						{
						_assignedNodes.Add(ln);
						ln.tLight = this;
						break;
						}
					}
				}

			// Alte Versionen konnten nur einen Node pro TrafficLight haben und waren daher anders referenziert, auch darum wollen wir uns kümmern:
			if (saveVersion <= 2)
				{
				foreach (LineNode ln in nodesList)
					{
					if (ln.GetHashCode() == parentNodeHash)
						{
						AddAssignedLineNode(ln);
						break;
						}
					}
				}
			}
        #endregion

		/// <summary>
        /// сообщает LineNode ln при TrafficLight таким путем, что он знает что он с ним связан
		/// </summary>
		/// <param name="ln">заявляемый LineNode</param>
		public void AddAssignedLineNode(LineNode ln)
			{
			_assignedNodes.Add(ln);
			ln.tLight = this;
			}

		/// <summary>
        /// сообщает LineNode ln при TrafficLight, так что он знает, что он больше не связан с ним
		/// </summary>
        /// <param name="ln">anzumeldender LineNode</param>
        /// <returns>true, если процесс выхода из системы успешен, в противном случае false</returns>
		public bool RemoveAssignedLineNode(LineNode ln)
			{
			if (ln != null)
				{
				ln.tLight = null;
				return _assignedNodes.Remove(ln);
				}
			return false;
			}

		/// <summary>
		/// загорелся зеленый цвет светофора
		/// </summary>
		public void SwitchToGreen()
			{
			this.trafficLightState = State.GREEN;
			}
		/// <summary>
        /// загорелся красный цвет светофора
		/// </summary>
		public void SwitchToRed()
			{
			this.trafficLightState = State.RED;
			}


		/// <summary>
		/// отчетность от TrafficLight на относящемуся ему LineNodes, таким образом, что TrafficLight может быть безапасно удален.
		/// </summary>
		public override void Dispose()
			{
			base.Dispose();

			while (_assignedNodes.Count > 0)
				{
				RemoveAssignedLineNode(_assignedNodes[0]);
				}
			}

		}
    }

﻿/*
 *  CityTrafficSimulator - a tool to simulate traffic in urban areas and on intersections
 *  Copyright (C) 2005-2014, Christian Schulte zu Berge
 *  
 *  This program is free software; you can redistribute it and/or modify it under the 
 *  terms of the GNU General Public License as published by the Free Software 
 *  Foundation; either version 3 of the License, or (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful, but WITHOUT ANY 
 *  WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A 
 *  PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License along with this 
 *  program; if not, see <http://www.gnu.org/licenses/>.
 * 
 *  Web:  http://www.cszb.net
 *  Mail: software@cszb.net
 */


using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using CityTrafficSimulator.Tools.ObserverPattern;

namespace CityTrafficSimulator.Timeline
	{
	/// <summary>
	/// Klasse zur Steuerung der Timeline. Im Speziellen also Steuerung der TrafficLights.
	/// </summary>
	public class TimelineManagment
		{

		#region Variablen

		/// <summary>
		/// aktuelle Position der Timeline
		/// </summary>
		private double _currentTime;
		/// <summary>
		/// aktuelle Position der Timeline
		/// </summary>
		public double CurrentTime
			{
			get { return _currentTime; }
			set { _currentTime = value; OnCurrentTimeChanged(new CurrentTimeChangedEventArgs(_currentTime)); }
			}

		/// <summary>
		/// Umlaufzeit / maximale Zeit (Länge) der Timeline
		/// </summary>
		private double _maxTime = 20;
		/// <summary>
		/// Umlaufzeit / maximale Zeit (Länge) der Timeline
		/// </summary>
		public double maxTime
			{
			get { return _maxTime; }
			set { _maxTime = value; UpdateMaxTime(); OnMaxTimeChanged(); }
			}


		/// <summary>
		/// Liste der TimelineGroups
		/// </summary>
		private List<TimelineGroup> _groups = new List<TimelineGroup>();
		/// <summary>
		/// Liste der TimelineGroups
		/// </summary>
		public List<TimelineGroup> groups
			{
			get { return _groups; }
			set { _groups = value; OnGroupsChanged(); }
			}


		/// <summary>
		/// Summe aller TimelineEntries in allen TimelineGroups
		/// </summary>
		private int _totalEntryCount;

		#endregion

		#region Prozeduren


		#region Entitätengedöns

		/// <summary>
		/// fügt der Timeline ein fertiges TimelineEvent in die Gruppe parentGroup hinzu
		/// </summary>
		/// <param name="entryToAdd">TimelineEntry welcher eingefügt werden soll</param>
		public void AddEntry(TimelineEntry entryToAdd)
			{
			entryToAdd.maxTime = maxTime;
			entryToAdd.EntryChanged += new TimelineEntry.EntryChangedEventHandler(entryToAdd_EntryChanged);
			entryToAdd.parentGroup.AddEntry(entryToAdd);

			_totalEntryCount++;
			OnGroupsChanged();
			}

		void entryToAdd_EntryChanged(object sender, TimelineEntry.EntryChangedEventArgs e)
			{
			OnEntryChanged(new EntryChangedEventArgs(e.affectedEntry));
			}

		/// <summary>
		/// entfernt das TimelineEntry te
		/// </summary>
		/// <param name="te">zu entfernendes TimelineEntry</param>
		public void RemoveEntry(TimelineEntry te)
			{
			te.EntryChanged -= entryToAdd_EntryChanged;
			te.parentGroup.entries.Remove(te);

			_totalEntryCount--;
			OnGroupsChanged();
			}

		/// <summary>
		/// fügt der Timeline eine neue TimelineGroup hinzu
		/// </summary>
		/// <param name="tg">hinzuzufügende TimelineGroup</param>
		public void AddGroup(TimelineGroup tg)
			{
			tg.GroupChanged += new TimelineGroup.GroupChangedEventHandler(tg_GroupChanged);
			_groups.Add(tg);
			OnGroupsChanged();
			}

		void tg_GroupChanged(object sender, EventArgs e)
			{
			OnGroupsChanged();
			}

		/// <summary>
		/// entfernt die TimelineGroup g
		/// </summary>
		/// <param name="g">zu entfernende TimelineGroup</param>
		public void RemoveGroup(TimelineGroup g)
			{
			if (g.entries.Count == 0)
				{
				g.GroupChanged -= tg_GroupChanged;
				_groups.Remove(g);
				OnGroupsChanged();
				}
			}


		/// <summary>
		/// entfernt alle TimelineGroups und alle darin enthaltenden TimelineEntries
		/// </summary>
		public void Clear()
			{
			foreach (TimelineGroup tg in _groups)
				{
				while (tg.entries.Count > 0)
					{
					RemoveEntry(tg.entries[0]);
					}
				}
			_groups.Clear();
			OnGroupsChanged();
			}


		/// <summary>
		/// Updates maxTime in all contained entries.
		/// </summary>
		public void UpdateMaxTime()
			{
			foreach (TimelineGroup tg in _groups)
				{
				foreach (TimelineEntry te in tg.entries)
					{
					te.maxTime = _maxTime;
					}
				}
			}

		#endregion

		#region Zeitmanagement

		/// <summary>
		/// bewegt die Timeline um time weiter
		/// </summary>
		/// <param name="time">Zeit um die sich die Timeline weiterbewegen soll</param>
		public void Advance(double time)
			{
			_currentTime += time;
			if (_currentTime > _maxTime)
				{
				_currentTime = 0;
				}

			// Nun noch bei jedem TimelineEntry Advance() aufrufen
			foreach (TimelineGroup tg in _groups)
				{
				foreach (TimelineEntry te in tg.entries)
					{
					te.AdvanceTo(_currentTime);
					}
				}

			OnCurrentTimeChanged(new CurrentTimeChangedEventArgs(_currentTime));
			}

		/// <summary>
		/// bewegt die Timeline zum Zeitpunkt time 
		/// </summary>
		/// <param name="time">Zeit zu der sich die Timeline weiterbewegen soll</param>
		public void AdvanceTo(double time)
			{
			_currentTime = time % _maxTime;

			// Nun noch bei jedem TimelineEntry Advance() aufrufen
			foreach (TimelineGroup tg in _groups)
				{
				foreach (TimelineEntry te in tg.entries)
					{
					te.AdvanceTo(_currentTime);
					}
				}

			OnCurrentTimeChanged(new CurrentTimeChangedEventArgs(_currentTime));
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
				foreach (TimelineGroup tg in _groups)
					{
					tg.PrepareForSave();
					}

				xw.WriteStartElement("TrafficLights");

				// write cycle time
				xw.WriteStartAttribute("cycleTime");
				xw.WriteString(_maxTime.ToString());
				xw.WriteEndAttribute();

				// TimelineGroups (und damit auch alles was dahinter liegt) serialisieren 
				Type[] extraTypes = { typeof(TrafficLight) };
				XmlSerializer xs = new XmlSerializer(typeof(TimelineGroup), extraTypes);
				foreach (TimelineGroup tg in _groups)
					{
					xs.Serialize(xw, tg, xsn);
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
		/// <param name="nodesList">Liste von allen existierenden LineNodes</param>
		/// <param name="lf">LoadingForm für Statusinformationen</param>
		public void LoadFromFile(XmlDocument xd, List<LineNode> nodesList, LoadingForm.LoadingForm lf)
			{
			lf.SetupLowerProgess("Parsing XML...", 1);

			int saveVersion = 0;

			// erstma alles vorhandene löschen
			foreach (TimelineGroup tg in _groups)
				{
				foreach (TimelineEntry te in tg.entries)
					{
					// Löschen vorbereiten
					te.Dispose();
					}
				tg.entries.Clear();
				}
			_groups.Clear();

			XmlNode mainNode = xd.SelectSingleNode("//CityTrafficSimulator");
			XmlNode saveVersionNode = mainNode.Attributes.GetNamedItem("saveVersion");
			if (saveVersionNode != null)
				saveVersion = Int32.Parse(saveVersionNode.Value);
			else
				saveVersion = 0;

			lf.StepLowerProgress();

			if (saveVersion >= 4)
				{
				XmlNode xnlTrafficLights = xd.SelectSingleNode("//CityTrafficSimulator/TrafficLights");
				XmlNode cycleTimeNode = xnlTrafficLights.Attributes.GetNamedItem("cycleTime");
				if (cycleTimeNode != null)
					maxTime = Double.Parse(cycleTimeNode.Value);
				}
			else
				{
				maxTime = 50;
				}

			if (saveVersion >= 3)
				{
				// entsprechenden Node auswählen
				XmlNodeList xnlLineNode = xd.SelectNodes("//CityTrafficSimulator/TrafficLights/TimelineGroup");
				Type[] extraTypes = { typeof(TrafficLight) };
				foreach (XmlNode aXmlNode in xnlLineNode)
					{
					// Node in einen TextReader packen
					TextReader tr = new StringReader(aXmlNode.OuterXml);
					// und Deserializen
					XmlSerializer xs = new XmlSerializer(typeof(TimelineGroup), extraTypes);
					TimelineGroup tg = (TimelineGroup)xs.Deserialize(tr);

					// ab in die Liste
					tg.GroupChanged += new TimelineGroup.GroupChangedEventHandler(tg_GroupChanged);
					_groups.Add(tg);
					}
				}
			else
				{
				TimelineGroup unsortedGroup = new TimelineGroup("Unsorted Signals", false);

				// entsprechenden Node auswählen
				XmlNodeList xnlLineNode = xd.SelectNodes("//CityTrafficSimulator/Layout/LineNode/tLight");
				foreach (XmlNode aXmlNode in xnlLineNode)
					{
					// der XMLNode darf nicht tLight heißen, sondern muss TrafficLight heißen. Das müssen wir mal anpassen:
					XmlDocument doc = new XmlDocument();
					XmlElement elem = doc.CreateElement("TrafficLight");
					elem.InnerXml = aXmlNode.InnerXml;
					doc.AppendChild(elem);
					// so, das war nicht wirklich hübsch und schnell, aber es funktioniert ;)
					
					// Node in einen TextReader packen
					StringReader tr = new StringReader(doc.InnerXml);

					// und Deserializen
					XmlSerializer xs = new XmlSerializer(typeof(TrafficLight));
					TrafficLight tl = (TrafficLight)xs.Deserialize(tr);


					XmlNode pnhNode = doc.SelectSingleNode("//TrafficLight/parentNodeHash");
					tl.parentNodeHash = Int32.Parse(pnhNode.InnerXml);
					unsortedGroup.AddEntry(tl);
					}

				// ab in die Liste
				_groups.Add(unsortedGroup);
				}

			lf.SetupLowerProgess("Restoring Signals...", _groups.Count);

			// Abschließende Arbeiten: Referenzen auflösen
			foreach (TimelineGroup tg in _groups)
				{
				tg.RecoverFromLoad(saveVersion, nodesList);
				lf.StepLowerProgress();
				}

			OnGroupsChanged();
			}

		#endregion


		#region Eventhandler

		#region CurrentTimeChanged-Ereignis

		/// <summary>
		/// EventArgs die einem SelectionChanged-Ereignis mitgegeben wird
		/// </summary>
		public class CurrentTimeChangedEventArgs : EventArgs
			{
			/// <summary>
			/// Erstellt ein neues CurrentTimeChangedEventArgs
			/// </summary>
			/// <param name="currentTime">aktuelle Zeit nachdem currentTime geändert wurde</param>
			public CurrentTimeChangedEventArgs(double currentTime)
				{
				this.currentTime = currentTime;
				}

			/// <summary>
			/// aktuelle Zeit der timelineSteuerung, nachdem currentTime geändert wurde
			/// </summary>
			public double currentTime;
			}

		/// <summary>
		/// Delegate für einen EventHandler wenn die selektierte Gruppe/Entry geändert wurde
		/// </summary>
		/// <param name="sender">Absender des Events</param>
		/// <param name="e">Eventparameter</param>
		public delegate void CurrentTimeChangedEventHandler(object sender, CurrentTimeChangedEventArgs e);

		/// <summary>
		/// TimelineMoved Ereignis tritt auf, wenn die Timeline mit Hilfe dieses Controls bewegt wurde
		/// </summary>
		public event CurrentTimeChangedEventHandler CurrentTimeChanged;

		/// <summary>
		/// Hilfsfunktion zum Absetzten des TimelineMoved Events
		/// </summary>
		/// <param name="e">Eventparameter</param>
		protected void OnCurrentTimeChanged(CurrentTimeChangedEventArgs e)
			{
			if (CurrentTimeChanged != null)
				{
				CurrentTimeChanged(this, e);
				}
			}

		#endregion

		#region MaxTimeChanged-Ereignis

		/// <summary>
		/// Delegate für einen EventHandler wenn die selektierte Gruppe/Entry geändert wurde
		/// </summary>
		/// <param name="sender">Absender des Events</param>
		/// <param name="e">Eventparameter</param>
		public delegate void MaxTimeChangedEventHandler(object sender, EventArgs e);

		/// <summary>
		/// TimelineMoved Ereignis tritt auf, wenn die Timeline mit Hilfe dieses Controls bewegt wurde
		/// </summary>
		public event MaxTimeChangedEventHandler MaxTimeChanged;

		/// <summary>
		/// Hilfsfunktion zum Absetzten des TimelineMoved Events
		/// </summary>
		protected void OnMaxTimeChanged()
			{
			if (MaxTimeChanged != null)
				{
				MaxTimeChanged(this, new EventArgs());
				}
			}

		#endregion

		#region GroupsChanged-Ereignis

		/// <summary>
		/// Delegate für einen EventHandler wenn etwas an TimelineSteuerung.groups geändert wurde
		/// </summary>
		/// <param name="sender">Absender des Events</param>
		/// <param name="e">Eventparameter</param>
		public delegate void GroupsChangedEventHandler(object sender, EventArgs e);

		/// <summary>
		/// GroupsChanged Ereignis tritt auf, wenn etwas an TimelineSteuerung.groups geändert wurde
		/// </summary>
		public event GroupsChangedEventHandler GroupsChanged;

		/// <summary>
		/// Hilfsfunktion zum Absetzten des GroupsChanged Events
		/// </summary>
		protected void OnGroupsChanged()
			{
			if (GroupsChanged != null)
				{
				GroupsChanged(this, new EventArgs());
				}
			}

		#endregion

		#region EntryChanged-Ereignis

		/// <summary>
		/// EventArgs die einem EntryChanged-Ereignis mitgegeben wird
		/// </summary>
		public class EntryChangedEventArgs : EventArgs
			{
			/// <summary>
			/// Erstellt ein neues SelectionChangedEventArgs
			/// </summary>
			/// <param name="te"></param>
			public EntryChangedEventArgs(TimelineEntry te)
				{
				this.m_affectedEntry = te;
				}

			/// <summary>
			/// beteiligtes TimelineEntry
			/// </summary>
			private TimelineEntry m_affectedEntry;
			/// <summary>
			/// beteiligtes TimelineEntry
			/// </summary>
			public TimelineEntry affectedEntry
				{
				get { return m_affectedEntry; }
				}
			}

		/// <summary>
		/// Delegate für einen EventHandler wenn ein TimelineEntry geändert wurde
		/// </summary>
		/// <param name="sender">Absender des Events</param>
		/// <param name="e">Eventparameter</param>
		public delegate void EntryChangedEventHandler(object sender, EntryChangedEventArgs e);

		/// <summary>
		/// EntryChanged Ereignis tritt auf, wenn ein TimelineEntry geändert wurde
		/// </summary>
		public event EntryChangedEventHandler EntryChanged;

		/// <summary>
		/// Hilfsfunktion zum Absetzten des EntryChanged Events
		/// </summary>
		/// <param name="e">Eventparameter</param>
		protected void OnEntryChanged(EntryChangedEventArgs e)
			{
			if (EntryChanged != null)
				{
				EntryChanged(this, e);
				}
			}


		#endregion

		#endregion

		}
	}

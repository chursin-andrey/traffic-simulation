/*
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

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using CityTrafficSimulator.Timeline;
using CityTrafficSimulator.Tools.ObserverPattern;

namespace CityTrafficSimulator.Timeline
	{
	public partial class TrafficLightForm : Form
		{

		/// <summary>
		/// Flag, ob gerade Notify() aufgerufen wurde
		/// </summary>
		private bool isNotified = false;

		/// <summary>
		/// Flag, ob gerade OnEventChanged() gefeuert wurde
		/// </summary>
		private bool changedEvent = false;

		/// <summary>
		/// zugeordnete TimelineSteuerung
		/// </summary>
		private TimelineManagment steuerung;

		/// <summary>
		/// selected entry in timeline control
		/// </summary>
		public TimelineEntry selectedEntry
			{
			get { return timelineControl.selectedEntry; }
			set { timelineControl.selectedEntry = value; }
			}


		/// <summary>
		/// Standardkonstruktor
		/// </summary>
		/// <param name="steuerung">TimelineSteuerung die die Informationen zur Anzeige enthält</param>
		public TrafficLightForm(TimelineManagment steuerung)
			{
			this.steuerung = steuerung;
			InitializeComponent();
			this.splitContainer1.Panel2MinSize = 220;
			timelineControl.steuerung = steuerung;
			steuerung.GroupsChanged += new TimelineManagment.GroupsChangedEventHandler(steuerung_GroupsChanged);
			steuerung.MaxTimeChanged += new TimelineManagment.MaxTimeChangedEventHandler(steuerung_MaxTimeChanged);
			timelineControl.SelectionChanged += new TimelineControl.SelectionChangedEventHandler(timelineControl_SelectionChanged);
			cycleTimeSpinEdit.Value = (decimal)steuerung.maxTime;
			}

		void timelineControl_SelectionChanged(object sender, TimelineControl.SelectionChangedEventArgs e)
			{
			isNotified = true;
			if (timelineControl.selectedEntry != null)
				{
				trafficLightNameEdit.Text = timelineControl.selectedEntry.name;
				groupComboBox.SelectedItem = timelineControl.selectedEntry.parentGroup;
				}

			if (timelineControl.selectedGroup != null)
				{
				groupTitleEdit.Text = timelineControl.selectedGroup.title;
				if (timelineControl.selectedEntry == null)
					{
					groupComboBox.SelectedItem = timelineControl.selectedGroup;
					}
				}
			OnSelectedEntryChanged(new SelectedEntryChangedEventArgs());
			isNotified = false;
			}

		void steuerung_GroupsChanged(object sender, EventArgs e)
			{
			isNotified = true;
			UpdateGroupComboBox();
			isNotified = false;
			}

		void steuerung_MaxTimeChanged(object sender, EventArgs e)
			{
			isNotified = true;
			cycleTimeSpinEdit.Value = (decimal)steuerung.maxTime;
			isNotified = false;
			}
		
		private void UpdateGroupComboBox()
			{
			groupComboBox.Items.Clear();

			foreach (TimelineGroup tg in steuerung.groups)
				{
				groupComboBox.Items.Add(tg);
				}

			groupComboBox.SelectedItem = timelineControl.selectedGroup;
			}


		private void addGroupButton_Click(object sender, EventArgs e)
			{
			timelineControl.selectedEntry = null;
			TimelineGroup tg = new TimelineGroup(groupTitleEdit.Text, false);
			if (tg.title == "")
				{
				tg.title = "Gruppe " + (steuerung.groups.Count + 1).ToString();
				}
			steuerung.AddGroup(tg);
			}


		private void removeGroupButton_Click(object sender, EventArgs e)
			{
			if (timelineControl.selectedGroup != null)
				{
				if (timelineControl.selectedGroup.entries.Count == 0)
					{
					timelineControl.selectedEntry = null;
					steuerung.RemoveGroup(timelineControl.selectedGroup);
					}
				else
					{
					MessageBox.Show("Gruppe " + timelineControl.selectedGroup.title + " ist nicht leer!");
					}
				}
			}

		private void groupTitleEdit_TextChanged(object sender, EventArgs e)
			{
			if (timelineControl.selectedGroup != null)
				{
				timelineControl.selectedGroup.title = groupTitleEdit.Text;
				timelineControl.Invalidate();
				}
			}

		private void trafficLightNameEdit_TextChanged(object sender, EventArgs e)
			{
			if (timelineControl.selectedEntry != null)
				{
				timelineControl.selectedEntry.name = trafficLightNameEdit.Text;
				timelineControl.Invalidate();
				}
			}

		private void addTrafficLightButton_Click(object sender, EventArgs e)
			{
			TimelineGroup parentGroup = groupComboBox.SelectedItem as TimelineGroup;

			if (parentGroup != null)
				{
				TimelineEntry te = new TrafficLight();
				te.parentGroup = parentGroup;
				te.name = trafficLightNameEdit.Text;
				if (te.name == "")
					{
					te.name = "LSA " + (parentGroup.entries.Count + 1).ToString();
					}
				steuerung.AddEntry(te);
				}
			else
				{
				MessageBox.Show("Keine Gruppe ausgewählt!");
				}
			}

		private void removeTrafficLightButton_Click(object sender, EventArgs e)
			{
			if (timelineControl.selectedEntry != null)
				{
				steuerung.RemoveEntry(timelineControl.selectedEntry);
				}
			}

		private void groupComboBox_SelectedIndexChanged(object sender, EventArgs e)
			{
			if (! isNotified && timelineControl.selectedEntry != null)
				{
				TimelineGroup newGroup = groupComboBox.SelectedItem as TimelineGroup;
				TimelineEntry te = timelineControl.selectedEntry;
				steuerung.RemoveEntry(te);
				te.parentGroup = newGroup;
				steuerung.AddEntry(te);
				}

			}

		private void numericUpDown1_ValueChanged(object sender, EventArgs e)
			{
			timelineControl.zoom = (int) zoomSpinEdit.Value;
			}

		private void timelineControl_MouseMove(object sender, MouseEventArgs e)
			{
			if (changedEvent)
				{
				changedEvent = false;
				}
			else
				{
				statusLabel.Text = "Zeitleiste Mausposition: " + timelineControl.GetTimeAtControlPosition(e.Location, false).ToString() + "s";
				}
			}

		private void timelineControl_KeyDown(object sender, KeyEventArgs e)
			{
			}

		private void timelineControl_KeyPress(object sender, KeyPressEventArgs e)
			{

			}

		private void timelineControl_KeyUp(object sender, KeyEventArgs e)
			{
			if (e.KeyCode == Keys.Delete && timelineControl.selectedEntry != null)
				{
				removeTrafficLightButton_Click(this, new EventArgs());
				}
			
			}

		private void timelineControl_EventChanged(object sender, TimelineControl.EventChangedEventArgs e)
			{
			switch (e.dragAction)
				{
				case TimelineControl.DragNDrop.MOVE_EVENT:
					statusLabel.Text = "verschiebe Event, Start: " + e.handeledEvent.eventTime + "s, Ende: " + (e.handeledEvent.eventTime + e.handeledEvent.eventLength) + "s";
					changedEvent = true;
					break;
				case TimelineControl.DragNDrop.MOVE_EVENT_START:
					statusLabel.Text = "verschiebe Event-Start: " + e.handeledEvent.eventTime + "s";
					changedEvent = true;
					break;
				case TimelineControl.DragNDrop.MOVE_EVENT_END:
					statusLabel.Text = "verschiebe Event-Ende: " + (e.handeledEvent.eventTime + e.handeledEvent.eventLength) + "s";
					changedEvent = true;
					break;
				default:
					break;
				}
			}

		private void cycleTimeSpinEdit_ValueChanged(object sender, EventArgs e)
			{
			if (! isNotified)
				steuerung.maxTime = (double)cycleTimeSpinEdit.Value;
			}

		#region SelectedEntryChanged event

		/// <summary>
		/// EventArgs for a SelectedEntryChanged event
		/// </summary>
		public class SelectedEntryChangedEventArgs : EventArgs
			{
			/// <summary>
			/// Creates new SelectedEntryChangedEventArgs
			/// </summary>
			public SelectedEntryChangedEventArgs()
				{
				}
			}

		/// <summary>
		/// Delegate for the SelectedEntryChanged-EventHandler, which is called when the selected TimelineEntry of the TimelineControl has changed
		/// </summary>
		/// <param name="sender">Sneder of the event</param>
		/// <param name="e">Event parameter</param>
		public delegate void SelectedEntryChangedEventHandler(object sender, SelectedEntryChangedEventArgs e);

		/// <summary>
		/// The SelectedEntryChanged event occurs when the selected TimelineEntry of the TimelineControl has changed
		/// </summary>
		public event SelectedEntryChangedEventHandler SelectedEntryChanged;

		/// <summary>
		/// Helper method to initiate the SelectedEntryChanged event
		/// </summary>
		/// <param name="e">Event parameters</param>
		protected void OnSelectedEntryChanged(SelectedEntryChangedEventArgs e)
			{
			if (SelectedEntryChanged != null)
				{
				SelectedEntryChanged(this, e);
				}
			}

		#endregion

		private void groupsGroupBox_SizeChanged(object sender, EventArgs e)
			{
			splitContainer1.SplitterDistance = splitContainer1.ClientSize.Width - groupsGroupBox.Width - 32;
			}

		private void button1_Click(object sender, EventArgs e)
			{
			if (timelineControl.selectedGroup != null)
				{
				timelineControl.selectedGroup.UpdateConflictPoints();
				}
			}

		}
	}

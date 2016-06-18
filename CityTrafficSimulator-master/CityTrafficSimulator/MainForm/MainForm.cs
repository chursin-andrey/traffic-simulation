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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using Crownwood.Magic.Common;
using Crownwood.Magic.Docking;

using CityTrafficSimulator.Timeline;
using CityTrafficSimulator.Vehicle;
using CityTrafficSimulator.Tools;


namespace CityTrafficSimulator
    {
	/// <summary>
	/// Hauptformular von CityTrafficSimulator
	/// </summary>
	public partial class MainForm : Form
		{
		#region Docking stuff

		/// <summary>
		/// The one and only holy DockingManager
		/// </summary>
		private Crownwood.Magic.Docking.DockingManager _dockingManager;

		private void SetContentDefaultSettings(Content c, Size s)
			{
			c.DisplaySize = s;
			c.FloatingSize = s;
			c.AutoHideSize = s;
			c.CloseButton = false;
			}

		private void SetupDockingStuff()
			{
			// Setup main canvas
			_dockingManager.InnerControl = pnlMainGrid;

			// Setup right docks: Most setup panels
			Content connectionContent = _dockingManager.Contents.Add(pnlConnectionSetup, "Network Setup");
			SetContentDefaultSettings(connectionContent, pnlConnectionSetup.Size);
			Content networkContent = _dockingManager.Contents.Add(pnlNetworkInfo, "Network Info");
			SetContentDefaultSettings(networkContent, pnlNetworkInfo.Size);
			Content signalContent = _dockingManager.Contents.Add(pnlSignalAssignment, "Signal Assignment");
			SetContentDefaultSettings(signalContent, pnlSignalAssignment.Size);
			Content viewContent = _dockingManager.Contents.Add(pnlRenderSetup, "Render Setup");
			SetContentDefaultSettings(viewContent, pnlRenderSetup.Size);
			Content canvasContent = _dockingManager.Contents.Add(pnlCanvasSetup, "Canvas Setup");
			SetContentDefaultSettings(canvasContent, pnlCanvasSetup.Size);
			Content simContent = _dockingManager.Contents.Add(pnlSimulationSetup, "Simulation Setup");
			SetContentDefaultSettings(simContent, pnlSimulationSetup.Size);
			Content thumbContent = _dockingManager.Contents.Add(thumbGrid, "Thumbnail View");
			SetContentDefaultSettings(thumbContent, new Size(150, 150));
			Content statisticsContent = _dockingManager.Contents.Add(pnlStatistics, "Connection Statistics");
			SetContentDefaultSettings(statisticsContent, new Size(196, 196));
			Content layersContent = _dockingManager.Contents.Add(pnlLayers, "Network Layers");
			SetContentDefaultSettings(layersContent, new Size(196, 64));
			
			WindowContent dock0 = _dockingManager.AddContentWithState(connectionContent, State.DockRight);
			_dockingManager.AddContentToWindowContent(signalContent, dock0);

			WindowContent dock1 = _dockingManager.AddContentToZone(networkContent, dock0.ParentZone, 1) as WindowContent;
			_dockingManager.AddContentToWindowContent(simContent, dock1);

			WindowContent dock2 = _dockingManager.AddContentToZone(thumbContent, dock0.ParentZone, 2) as WindowContent;
			_dockingManager.AddContentToWindowContent(layersContent, dock2); 
			_dockingManager.AddContentToWindowContent(viewContent, dock2); 
			_dockingManager.AddContentToWindowContent(canvasContent, dock2);
			_dockingManager.AddContentToWindowContent(statisticsContent, dock2);


			// Setup bottom docks: TrafficLightForm, TrafficVolumeForm, pnlTimeline
			trafficLightForm = new TrafficLightForm(timelineSteuerung);
			trafficVolumeForm = new Verkehr.TrafficVolumeForm(trafficVolumeSteuerung, this, nodeSteuerung);

			Content tlfContent = _dockingManager.Contents.Add(trafficLightForm, "Signal Editor");
			SetContentDefaultSettings(tlfContent, trafficLightForm.Size);
			Content tvfContent = _dockingManager.Contents.Add(trafficVolumeForm, "Traffic Volume Editor");
			SetContentDefaultSettings(tvfContent, trafficVolumeForm.Size);

			WindowContent bottomDock = _dockingManager.AddContentWithState(tlfContent, State.DockBottom);
			_dockingManager.AddContentToWindowContent(tvfContent, bottomDock);

			if (File.Exists("GUILayout.xml"))
				{
				try
					{
					_dockingManager.LoadConfigFromFile("GUILayout.xml");
					}
				catch (System.Xml.XmlException)
					{
					// do nothing here
					}
				}

			_dockingManager.ContentHiding += new DockingManager.ContentHidingHandler(_dockingManager_ContentHiding);
			statusleiste.Visible = false;

			foreach (Content c in _dockingManager.Contents)
				{
				if (!c.Visible)
					c.BringToFront();
				}
			}

		void _dockingManager_ContentHiding(Content c, CancelEventArgs cea)
			{
			cea.Cancel = true;
			}

		#endregion

		#region Hilfsklassen

		/// <summary>
		/// stores the window state
		/// </summary>
		[Serializable]
		public struct WindowSettings
			{
			/// <summary>
			/// Window state
			/// </summary>
			public FormWindowState _windowState;
			/// <summary>
			/// Position of window
			/// </summary>
			public Point _position;
			/// <summary>
			/// Size of window
			/// </summary>
			public Size _size;
			}

		private enum DragNDrop
			{
			NONE,
			MOVE_MAIN_GRID,
			MOVE_NODES, 
			CREATE_NODE, 
			MOVE_IN_SLOPE, MOVE_OUT_SLOPE, 
			MOVE_TIMELINE_BAR, MOVE_EVENT, MOVE_EVENT_START, MOVE_EVENT_END, 
			MOVE_THUMB_RECT,
			DRAG_RUBBERBAND
			}

		/// <summary>
		/// MainForm invalidation level
		/// </summary>
		public enum InvalidationLevel
			{
			/// <summary>
			/// invalidate everything
			/// </summary>
			ALL,
			/// <summary>
			/// invalidate only main canvas
			/// </summary>
			ONLY_MAIN_CANVAS,
			/// <summary>
			/// invalidate main canvas and timeline
			/// </summary>
			MAIN_CANVAS_AND_TIMLINE
			}

		#endregion

		#region Variablen / Properties

		#region NetworkLayer GUI stuff

		private struct NetworkLayerGUI
			{
			#region Members

			/// <summary>
			/// Reference to the NodeSteuerung controlling the network layers
			/// </summary>
			private NodeSteuerung _nodeController;

			/// <summary>
			/// Reference to base NetworkLayer
			/// </summary>
			private NetworkLayer _networkLayer;

			/// <summary>
			/// Textbox for editing layer title
			/// </summary>
			public System.Windows.Forms.TextBox _editTitle;

			/// <summary>
			/// Button to remove layer
			/// </summary>
			public System.Windows.Forms.Button _btnRemove;

			/// <summary>
			/// Checkbox for layer visible state
			/// </summary>
			public System.Windows.Forms.CheckBox _cbVisible;

			#endregion

			#region Constructor & event handlers

			/// <summary>
			/// Constructor: Creates the necessary GUI elements for the given NetworkLayer
			/// </summary>
			/// <param name="ns">Reference to the NodeSteuerung controlling the network layers</param>
			/// <param name="nl">Reference to base NetworkLayer to create GUI elements for</param>
			public NetworkLayerGUI(NodeSteuerung ns, NetworkLayer nl)
				{
				_nodeController = ns;
				_networkLayer = nl;
				_editTitle = new System.Windows.Forms.TextBox();
				_btnRemove = new System.Windows.Forms.Button();
				_cbVisible = new System.Windows.Forms.CheckBox();

				_editTitle.Text = nl.title;
				_editTitle.Dock = DockStyle.Fill;
				_editTitle.Leave += new EventHandler(_editTitle_Leave);

				_btnRemove.Text = "Remove";
				_btnRemove.Click += new EventHandler(_btnRemove_Click);

				_cbVisible.Text = "Visible";
				_cbVisible.Checked = nl.visible;
				_cbVisible.CheckedChanged += new EventHandler(_cbVisible_CheckedChanged);
				}

			void _editTitle_Leave(object sender, EventArgs e)
				{
				_networkLayer.title = _editTitle.Text;
				}

			void _btnRemove_Click(object sender, EventArgs e)
				{
				if (! _nodeController.RemoveNetworkLayer(_networkLayer))
					{
					MessageBox.Show("Could not remove layer '" + _networkLayer.title + "' because it is assigned to at least one node.");
					}
				}

			void _cbVisible_CheckedChanged(object sender, EventArgs e)
				{
				_networkLayer.visible = _cbVisible.Checked;
				}

			#endregion

			#region methods

			public void AddToPanel(TableLayoutPanel tlp, int row)
				{
				tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
				tlp.Controls.Add(_editTitle, 0, row);
				tlp.Controls.Add(_btnRemove, 1, row);
				tlp.Controls.Add(_cbVisible, 2, row);
				}

			#endregion
			}

		private List<NetworkLayerGUI> _networkLayerGUI = new List<NetworkLayerGUI>();

		private void cbNetworkLayer_SelectionChangeCommitted(object sender, EventArgs e)
			{
			NetworkLayer nl = cbNetworkLayer.SelectedItem as NetworkLayer;
			if (nl != null)
				{
				foreach (LineNode ln in selectedLineNodes)
					{
					ln.networkLayer = nl;
					foreach (NodeConnection nc in ln.nextConnections)
						nodeSteuerung.FindIntersections(nc);
					foreach (NodeConnection nc in ln.prevConnections)
						nodeSteuerung.FindIntersections(nc);
					}
				}
			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		private void pnlLayers_Resize(object sender, EventArgs e)
			{
			tlpLayers.ColumnStyles[0].SizeType = SizeType.Absolute;
			tlpLayers.ColumnStyles[0].Width = tlpLayers.ClientSize.Width - 150;
			tlpLayers.ColumnStyles[1].SizeType = SizeType.Absolute;
			tlpLayers.ColumnStyles[1].Width = 65;
			tlpLayers.ColumnStyles[2].SizeType = SizeType.Absolute;
			tlpLayers.ColumnStyles[2].Width = 65;
			btnAddLayer.Location = new System.Drawing.Point(pnlLayers.ClientSize.Width - btnAddLayer.Width - 24, tlpLayers.Height + 8);
			}

		private void pnlLayers_Resize_1(object sender, EventArgs e)
			{
			btnAddLayer.Location = new System.Drawing.Point(pnlLayers.ClientSize.Width - btnAddLayer.Width - 24, tlpLayers.Height + 8);
			}

		private void btnAddLayer_Click(object sender, EventArgs e)
			{
			nodeSteuerung.AddNetworkLayer("Layer " + (nodeSteuerung._networkLayers.Count + 1).ToString(), true);
			}

		void nodeSteuerung_NetworkLayersChanged(object sender, NodeSteuerung.NetworkLayersChangedEventArgs e)
			{
			switch (e._invalidationLevel)
				{
				case NodeSteuerung.NetworkLayersChangedEventArgs.InvalidationLevel.ONLY_VISIBILITY_CHANGED:
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					break;
				default:
					// fist: update NetworkLayer combo box for LineNode setup
					cbNetworkLayer.Items.Clear();
					foreach (NetworkLayer nl in nodeSteuerung._networkLayers)
						{
						cbNetworkLayer.Items.Add(nl);
						}

					selectedLineNodes = selectedLineNodes;


					// then: update NetworkLayerGUI elements
					_networkLayerGUI.Clear();

					tlpLayers.SuspendLayout();
					tlpLayers.Controls.Clear();
					tlpLayers.RowStyles.Clear();

					tlpLayers.RowCount = nodeSteuerung._networkLayers.Count + 1;
					tlpLayers.Height = (tlpLayers.RowCount + 1) * 28;
					int i = 0;
					foreach (NetworkLayer nl in nodeSteuerung._networkLayers)
						{
						NetworkLayerGUI nlg = new NetworkLayerGUI(nodeSteuerung, nl);
						nlg.AddToPanel(tlpLayers, i);
						_networkLayerGUI.Add(nlg);
						++i;
						}

					tlpLayers.RowStyles.Add(new RowStyle(SizeType.AutoSize, 28));
					tlpLayers.ResumeLayout(true);
					break;
				}

			}
		#endregion

		private Image[,] satelliteImages;

		private bool dockToGrid = false;

		private NodeSteuerung.RenderOptions renderOptionsDaGrid = new NodeSteuerung.RenderOptions();
		private NodeSteuerung.RenderOptions renderOptionsThumbnail = new NodeSteuerung.RenderOptions();
		
		private Bitmap backgroundImage;
		private Bitmap resampledBackgroundImage;

		private Bitmap _connectionsRenderCacheBmp;
		private System.Drawing.TextureBrush _connectionsRenderCache;

		private DragNDrop howToDrag = DragNDrop.NONE;

		private Rectangle daGridRubberband;

		/// <summary>
		/// AutoscrollPosition vom daGrid umschließenden Panel. (Wird für Thumbnailanzeige benötigt)
		/// </summary>
		private Point daGridScrollPosition = new Point();

		/// <summary>
		/// Mittelpunkt der angezeigten Fläche in Weltkoordinaten. (Wird für Zoom benötigt)
		/// </summary>
		private PointF daGridViewCenter = new Point();

		private List<GraphicsPath> additionalGraphics = new List<GraphicsPath>();

		private float[,] zoomMultipliers = new float[,] {
			{ 0.1f, 10},
			{ 0.15f, 1f/0.15f},
			{ 0.2f, 5},
			{ 0.25f, 4},
			{ 1f/3f, 3},
			{ 0.5f, 2},
			{ 2f/3f, 1.5f},
			{ 1, 1},
			{ 1.5f, 2f/3f},
			{ 2, 0.5f}};

		private Point mouseDownPosition;


		/// <summary>
		/// Flag, ob OnAfterSelect() seine Arbeit tun darf
		/// </summary>
		private bool doHandleTrafficLightTreeViewSelect = true;


		/// <summary>
		/// currently selected start nodes for traffic volume
		/// </summary>
		private List<LineNode> m_fromLineNodes = new List<LineNode>();
		/// <summary>
		/// currently selected start nodes for traffic volume
		/// </summary>
		public List<LineNode> fromLineNodes
			{
			get { return m_fromLineNodes; }
			set { m_fromLineNodes = value; Invalidate(InvalidationLevel.ALL); }
			}

		/// <summary>
		/// currently selected destination nodes for traffic volume
		/// </summary>
		private List<LineNode> m_toLineNodes = new List<LineNode>();
		/// <summary>
		/// currently selected destination nodes for traffic volume
		/// </summary>
		public List<LineNode> toLineNodes
			{
			get { return m_toLineNodes; }
			set { m_toLineNodes = value; Invalidate(InvalidationLevel.ALL); }
			}


		/// <summary>
		/// Thumbnail Rectangle Client-Koordinaten
		/// </summary>
		private Rectangle thumbGridClientRect;

		/// <summary>
		/// vorläufige Standardgruppe für LSA
		/// </summary>
		private TimelineGroup unsortedGroup = new TimelineGroup("Unsorted Traffic Lights", false);


		/// <summary>
		/// NodeSteuerung
		/// </summary>
		public NodeSteuerung nodeSteuerung = new NodeSteuerung();

		/// <summary>
		/// TimelineSteuerung
		/// </summary>
		private TimelineSteuerung timelineSteuerung = new TimelineSteuerung();

		/// <summary>
		/// TrafficVolumeSteuerung
		/// </summary>
		private Verkehr.VerkehrSteuerung trafficVolumeSteuerung = new Verkehr.VerkehrSteuerung();

		/// <summary>
		/// Formular zur LSA-Steuerung
		/// </summary>
		private TrafficLightForm trafficLightForm;

		/// <summary>
		/// TrafficVolumeForm
		/// </summary>
		private Verkehr.TrafficVolumeForm trafficVolumeForm;


		/// <summary>
		/// speichert den ursprünglichen Slope beim Bearbeiten den In-/OutSlopes von LineNodes
		/// </summary>
		private List<Vector2> originalSlopes = new List<Vector2>();


		/// <summary>
		/// speichert die Positionen der vor dem Erstellen von neuen LineNodes selektierten LineNodes
		/// </summary>
		private List<Vector2> previousSelectedNodePositions = new List<Vector2>();

		/// <summary>
		/// speichert die relativen Positionsangaben der selektierten LineNodes zur Clickposition beim Drag'n'Drop bzw. beim erstellen von LineNodes
		/// </summary>
		private List<Vector2> selectedLineNodesMovingOffset = new List<Vector2>();

		/// <summary>
		/// aktuell ausgewählter LineNode
		/// </summary>
		private List<LineNode> m_selectedLineNodes = new List<LineNode>();
		/// <summary>
		/// aktuell ausgewählter LineNode
		/// </summary>
		public List<LineNode> selectedLineNodes
			{
			get { return m_selectedLineNodes; }
			set
				{
				m_selectedLineNodes = value;
				if (m_selectedLineNodes != null && m_selectedLineNodes.Count == 1)
					{
					// TrafficLight Einstellungen in die Form setzen
					if (m_selectedLineNodes[0].tLight != null)
						{
						doHandleTrafficLightTreeViewSelect = false;
						trafficLightTreeView.SelectNodeByTimelineEntry(m_selectedLineNodes[0].tLight);
						trafficLightTreeView.Select();
						trafficLightForm.selectedEntry = m_selectedLineNodes[0].tLight;
						doHandleTrafficLightTreeViewSelect = true;
						}
					else
						{
						trafficLightTreeView.SelectedNode = null;
						trafficLightForm.selectedEntry = null;
						}

					cbStopSign.Checked = m_selectedLineNodes[0].stopSign;
					selectedNodeConnection = null;
					}
				else
					{
					trafficLightTreeView.SelectedNode = null;
					trafficLightForm.selectedEntry = null;
					}

				// set network layer combo box to appropriate value
				if (m_selectedLineNodes.Count > 0)
					{
					cbStopSign.Checked = m_selectedLineNodes[0].stopSign;
					foreach (LineNode ln in m_selectedLineNodes)
						{
						cbStopSign.Checked &= ln.stopSign;
						}

					NetworkLayer tmp = m_selectedLineNodes[0].networkLayer;
					bool same = true;
					foreach (LineNode ln in m_selectedLineNodes)
						{
						if (ln.networkLayer != tmp)
							{
							same = false;
							break;
							}
						}

					if (same)
						cbNetworkLayer.SelectedItem = tmp;
					else
						cbNetworkLayer.SelectedItem = null;
					}
				/*else
					{
					NetworkLayer nl = cbNetworkLayer.SelectedItem as NetworkLayer;
					if (nl == null)
						cbNetworkLayer.SelectedItem = nodeSteuerung._networkLayers[0];
					}*/

				pnlStatistics.Invalidate();
				}
			}

		/// <summary>
		/// aktuell ausgewählte NodeConnection
		/// </summary>
		private NodeConnection m_selectedNodeConnection;
		/// <summary>
		/// aktuell ausgewählte NodeConnection
		/// </summary>
		public NodeConnection selectedNodeConnection
			{
			get { return m_selectedNodeConnection; }
			set
				{
				m_selectedNodeConnection = value;
				if (m_selectedNodeConnection != null)
					{
					// NodeConnection Einstellungen setzen
					nodeConnectionPrioritySpinEdit.Value = m_selectedNodeConnection.priority;
					carsAllowedCheckBox.Checked = m_selectedNodeConnection.carsAllowed;
					busAllowedCheckBox.Checked = m_selectedNodeConnection.busAllowed;
					tramAllowedCheckBox.Checked = m_selectedNodeConnection.tramAllowed;
					enableOutgoingLineChangeCheckBox.Checked = m_selectedNodeConnection.enableOutgoingLineChange;
					enableIncomingLineChangeCheckBox.Checked = m_selectedNodeConnection.enableIncomingLineChange;
					spinTargetVelocity.Value = (decimal)m_selectedNodeConnection.targetVelocity;

					selectedLineNodes.Clear();
					pnlStatistics.Invalidate();
					}
				}
			}

		/// <summary>
		/// aktuell ausgewähltes IVehicle
		/// </summary>
		private IVehicle m_selectedVehicle;
		/// <summary>
		/// aktuell ausgewähltes IVehicle
		/// </summary>
		public IVehicle selectedVehicle
			{
			get { return m_selectedVehicle; }
			set { m_selectedVehicle = value; }
			}


		/// <summary>
		/// Stopwatch zur Zeitmessung des renderings
		/// </summary>
		private System.Diagnostics.Stopwatch renderStopwatch = new System.Diagnostics.Stopwatch();

		/// <summary>
		/// Stopwatch zur Zeitmessung der Verkehrslogik
		/// </summary>
		private System.Diagnostics.Stopwatch thinkStopwatch = new System.Diagnostics.Stopwatch();

		#endregion

		/// <summary>
		/// Standardkonstruktor des Hauptformulars
		/// </summary>
		public MainForm()
			{

			timelineSteuerung.maxTime = 50;

			InitializeComponent();

			List<Color> tmp = new List<Color>();
			tmp.Add(Color.DarkRed);
			tmp.Add(Color.Yellow);
			tmp.Add(Color.DarkGreen);
			Tools.ColorMap cm = new Tools.ColorMap(tmp);
			cmcVelocityMapping.colormap = cm;

			_dockingManager = new Crownwood.Magic.Docking.DockingManager(this, VisualStyle.IDE);
			_dockingManager.SaveCustomConfig += new DockingManager.SaveCustomConfigHandler(_dockingManager_SaveCustomConfig);
			_dockingManager.LoadCustomConfig += new DockingManager.LoadCustomConfigHandler(_dockingManager_LoadCustomConfig);
			SetupDockingStuff();

			trafficLightTreeView.steuerung = timelineSteuerung;
			trafficLightForm.SelectedEntryChanged += new TrafficLightForm.SelectedEntryChangedEventHandler(trafficLightForm_SelectedEntryChanged);

			timelineSteuerung.CurrentTimeChanged += new TimelineSteuerung.CurrentTimeChangedEventHandler(timelineSteuerung_CurrentTimeChanged);
			timelineSteuerung.MaxTimeChanged += new TimelineSteuerung.MaxTimeChangedEventHandler(timelineSteuerung_MaxTimeChanged);

			nodeSteuerung.NetworkLayersChanged += new NodeSteuerung.NetworkLayersChangedEventHandler(nodeSteuerung_NetworkLayersChanged);
			nodeSteuerung.AddNetworkLayer("Layer 1", true);

			zoomComboBox.SelectedIndex = 7;
			daGridScrollPosition = new Point(0, 0);
			UpdateDaGridClippingRect();
			DaGrid.Dock = DockStyle.Fill;
			
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

			renderOptionsDaGrid.renderLineNodes = true;
			renderOptionsDaGrid.renderNodeConnections = true;
			renderOptionsDaGrid.renderVehicles = true;
			renderOptionsDaGrid.performClipping = true;
			renderOptionsDaGrid.clippingRect = new Rectangle(0, 0, 10000, 10000);
			renderOptionsDaGrid.renderIntersections = false;
			renderOptionsDaGrid.renderLineChangePoints = false;
			renderOptionsDaGrid.renderLineNodeDebugData = false;
			renderOptionsDaGrid.renderNodeConnectionDebugData = false;
			renderOptionsDaGrid.renderVehicleDebugData = false;

			renderOptionsThumbnail.renderLineNodes = false;
			renderOptionsThumbnail.renderNodeConnections = true;
			renderOptionsThumbnail.renderVehicles = false;
			renderOptionsThumbnail.performClipping = false;
			renderOptionsThumbnail.renderIntersections = false;
			renderOptionsThumbnail.renderLineChangePoints = false;
			renderOptionsThumbnail.renderLineNodeDebugData = false;
			renderOptionsThumbnail.renderNodeConnectionDebugData = false;
			renderOptionsThumbnail.renderVehicleDebugData = false;
			}

		void _dockingManager_LoadCustomConfig(XmlTextReader xmlIn)
			{
			if (xmlIn.Name == "WindowSettings")
				{
				XmlSerializer xs = new XmlSerializer(typeof(WindowSettings));
				WindowSettings ws = (WindowSettings)xs.Deserialize(xmlIn);
				if (ws._size.IsEmpty || ws._position.IsEmpty)
					{
					StartPosition = FormStartPosition.WindowsDefaultLocation;
					}
				else
					{
					Location = ws._position;
					Size = ws._size;
					StartPosition = FormStartPosition.Manual;
					WindowState = ws._windowState;
					}
				}
			}

		void _dockingManager_SaveCustomConfig(XmlTextWriter xmlOut)
			{
			WindowSettings ws = new WindowSettings();
			ws._windowState = WindowState;
			ws._position = Location;
			ws._size = Size;
			XmlSerializerNamespaces xsn = new XmlSerializerNamespaces();
			xsn.Add("", "");
			XmlSerializer xs = new XmlSerializer(typeof(WindowSettings));
			xs.Serialize(xmlOut, ws, xsn);
			}


		void timelineSteuerung_MaxTimeChanged(object sender, EventArgs e)
			{
			UpdateSimulationParameters();
			}

		void UpdateSimulationParameters()
			{
			GlobalTime.Instance.UpdateSimulationParameters(timelineSteuerung.maxTime, (double)stepsPerSecondSpinEdit.Value);
			nodeSteuerung.ResetAverageVelocities();
			}

		private void trafficLightForm_SelectedEntryChanged(object sender, TrafficLightForm.SelectedEntryChangedEventArgs e)
			{
			if (trafficLightForm.selectedEntry != null && doHandleTrafficLightTreeViewSelect)
				{
				TrafficLight tl = trafficLightForm.selectedEntry as TrafficLight;
				LineNode tmp = (m_selectedLineNodes.Count == 1) ? m_selectedLineNodes[0] : null;
				m_selectedLineNodes.Clear();
				m_selectedLineNodes.AddRange(tl.assignedNodes);
				selectedLineNodesMovingOffset.Clear();

				foreach (LineNode ln in m_selectedLineNodes)
					{
					if (tmp != null && ln != tmp)
						selectedLineNodesMovingOffset.Add(ln.position - tmp.position);
					else
						selectedLineNodesMovingOffset.Add(new Vector2(0, 0));
					}

				DaGrid.Invalidate();
				}
			}

		private void timelineSteuerung_CurrentTimeChanged(object sender, TimelineSteuerung.CurrentTimeChangedEventArgs e)
			{
			DaGrid.Invalidate();
			}

		#region Drag'n'Drop Gedöns

		#region thumbGrid
		private void thumbGrid_MouseMove(object sender, MouseEventArgs e)
			{
			// Mauszeiger ändern, falls über TimelineBar
			if (thumbGridClientRect.Contains(e.Location))
				{
				this.Cursor = Cursors.SizeAll;
				}
			else
				{
				this.Cursor = Cursors.Default;
				}

			if (howToDrag == DragNDrop.MOVE_THUMB_RECT)
				{
				thumbGridClientRect.X = e.Location.X + mouseDownPosition.X;
				thumbGridClientRect.Y = e.Location.Y + mouseDownPosition.Y;
				thumbGrid.Invalidate();
				}
			}

		private void thumbGrid_MouseLeave(object sender, EventArgs e)
			{
			this.Cursor = Cursors.Default;
			}

		private void thumbGrid_MouseDown(object sender, MouseEventArgs e)
			{
			if (!thumbGridClientRect.Contains(e.Location))
				{
				RectangleF bounds = nodeSteuerung.GetLineNodeBounds();
				float zoom = Math.Min(1.0f, Math.Min((float)thumbGrid.ClientSize.Width / bounds.Width, (float)thumbGrid.ClientSize.Height / bounds.Height));
				thumbGridClientRect.X = e.Location.X - thumbGridClientRect.Width / 2;
				thumbGridClientRect.Y = e.Location.Y - thumbGridClientRect.Height/2;

				daGridScrollPosition = new Point(
					(int)Math.Round((thumbGridClientRect.X / zoom) + bounds.X),
					(int)Math.Round((thumbGridClientRect.Y / zoom) + bounds.Y));


				UpdateDaGridClippingRect();
				thumbGrid.Invalidate();
				DaGrid.Invalidate(true);
				}

			mouseDownPosition = new Point(thumbGridClientRect.X - e.Location.X, thumbGridClientRect.Y - e.Location.Y);
			howToDrag = DragNDrop.MOVE_THUMB_RECT;

			}

		private void thumbGrid_MouseUp(object sender, MouseEventArgs e)
			{
			if (howToDrag == DragNDrop.MOVE_THUMB_RECT)
				{
				RectangleF bounds = nodeSteuerung.GetLineNodeBounds();
				float zoom = Math.Min(1.0f, Math.Min((float)thumbGrid.ClientSize.Width / bounds.Width, (float)thumbGrid.ClientSize.Height / bounds.Height));

				daGridScrollPosition = new Point(
					(int)Math.Round((thumbGridClientRect.X / zoom) + bounds.X),
					(int)Math.Round((thumbGridClientRect.Y / zoom) + bounds.Y));

				UpdateDaGridClippingRect();
				DaGrid.Invalidate(true);
				}
			howToDrag = DragNDrop.NONE;
			}

		#endregion

		#region DaGrid
		void DaGrid_MouseDown(object sender, MouseEventArgs e)
			{
			Vector2 clickedPosition = new Vector2(e.X, e.Y);
			clickedPosition *= zoomMultipliers[zoomComboBox.SelectedIndex, 1];
			clickedPosition += daGridScrollPosition;

			// Node Gedöns
			switch (e.Button)
				{
				case MouseButtons.Left:
					this.Cursor = Cursors.Default;

					#region Nodes hinzufügen
					if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
						{
						// LineNode hinzufügen
						List<LineNode> nodesToAdd = new List<LineNode>(m_selectedLineNodes.Count);
						selectedLineNodesMovingOffset.Clear();

						// testen ob ein Node schon markiert ist
						if (selectedLineNodes.Count > 0)
							{
							// Mittelpunkt des selektierten LineNodes ermitteln
							Vector2 midpoint = new Vector2(0, 0);
							foreach (LineNode ln in selectedLineNodes)
								{
								midpoint += ln.position;
								}
							midpoint *= (double)1 / selectedLineNodes.Count;

							// Line Node nach SelectedLineNode einfügen
							if (!((Control.ModifierKeys & Keys.Shift) == Keys.Shift))
								{
								// ersten Line Node erstellen
								nodesToAdd.Add(new LineNode(DaGrid.DockToGrid(clickedPosition, dockToGrid), cbNetworkLayer.SelectedItem as NetworkLayer, cbStopSign.Checked));
								selectedLineNodesMovingOffset.Add(m_selectedLineNodes[0].outSlope);

								// in/outSlope berechnen
								nodesToAdd[0].outSlope = 30 * Vector2.Normalize(nodesToAdd[0].position - midpoint);
								nodesToAdd[0].inSlope = -1 * nodesToAdd[0].outSlope;

								// Connecten
								nodeSteuerung.Connect(
									m_selectedLineNodes[0],
									nodesToAdd[0],
									(int)nodeConnectionPrioritySpinEdit.Value,
									(double)spinTargetVelocity.Value,
									carsAllowedCheckBox.Checked,
									busAllowedCheckBox.Checked,
									tramAllowedCheckBox.Checked,
									enableIncomingLineChangeCheckBox.Checked,
									enableOutgoingLineChangeCheckBox.Checked);


								// nun die restlichen LineNodes parallel erstellen
								for (int i = 1; i < m_selectedLineNodes.Count; i++)
									{
									Vector2 offset = m_selectedLineNodes[0].position - m_selectedLineNodes[i].position;
									selectedLineNodesMovingOffset.Add(offset);

									// Line Node erstellen
									nodesToAdd.Add(new LineNode(DaGrid.DockToGrid(clickedPosition - offset, dockToGrid), cbNetworkLayer.SelectedItem as NetworkLayer, cbStopSign.Checked));

									// in/outSlope berechnen
									nodesToAdd[i].outSlope = 30 * Vector2.Normalize(nodesToAdd[i].position - midpoint);
									nodesToAdd[i].inSlope = -1 * nodesToAdd[i].outSlope;

									// Connecten
									nodeSteuerung.Connect(
										m_selectedLineNodes[i],
										nodesToAdd[i],
										(int)nodeConnectionPrioritySpinEdit.Value,
										(double)spinTargetVelocity.Value,
										carsAllowedCheckBox.Checked,
										busAllowedCheckBox.Checked,
										tramAllowedCheckBox.Checked,
										enableIncomingLineChangeCheckBox.Checked,
										enableOutgoingLineChangeCheckBox.Checked);

									}
								}
							// Line Node vor SelectedLineNode einfügen
							else
								{
								// ersten Line Node erstellen
								nodesToAdd.Add(new LineNode(DaGrid.DockToGrid(clickedPosition, dockToGrid), cbNetworkLayer.SelectedItem as NetworkLayer, cbStopSign.Checked));
								selectedLineNodesMovingOffset.Add(m_selectedLineNodes[0].outSlope);


								// in/outSlope berechnen
								nodesToAdd[0].outSlope = 30 * Vector2.Normalize(nodesToAdd[0].position - midpoint);
								nodesToAdd[0].inSlope = -1 * nodesToAdd[0].outSlope;

								// Connecten
								nodeSteuerung.Connect(
									nodesToAdd[0],
									m_selectedLineNodes[0],
									(int)nodeConnectionPrioritySpinEdit.Value,
									(double)spinTargetVelocity.Value,
									carsAllowedCheckBox.Checked,
									busAllowedCheckBox.Checked,
									tramAllowedCheckBox.Checked,
									enableIncomingLineChangeCheckBox.Checked,
									enableOutgoingLineChangeCheckBox.Checked);


								// nun die restlichen LineNodes parallel erstellen
								for (int i = 1; i < m_selectedLineNodes.Count; i++)
									{
									Vector2 offset = m_selectedLineNodes[0].position - m_selectedLineNodes[i].position;
									selectedLineNodesMovingOffset.Add(offset);

									// Line Node erstellen
									nodesToAdd.Add(new LineNode(DaGrid.DockToGrid(clickedPosition - offset, dockToGrid), cbNetworkLayer.SelectedItem as NetworkLayer, cbStopSign.Checked));

									// in/outSlope berechnen
									nodesToAdd[i].outSlope = 30 * Vector2.Normalize(nodesToAdd[i].position - midpoint);
									nodesToAdd[i].inSlope = -1 * nodesToAdd[i].outSlope;

									// Connecten
									nodeSteuerung.Connect(
										nodesToAdd[i],
										m_selectedLineNodes[i],
										(int)nodeConnectionPrioritySpinEdit.Value,
										(double)spinTargetVelocity.Value,
										carsAllowedCheckBox.Checked,
										busAllowedCheckBox.Checked,
										tramAllowedCheckBox.Checked,
										enableIncomingLineChangeCheckBox.Checked,
										enableOutgoingLineChangeCheckBox.Checked);

									}
								}
							}
						else
							{
							// ersten Line Node erstellen
							nodesToAdd.Add(new LineNode(DaGrid.DockToGrid(clickedPosition, dockToGrid), cbNetworkLayer.SelectedItem as NetworkLayer, cbStopSign.Checked));
							selectedLineNodesMovingOffset.Add(new Vector2(0, 0));
							}

						previousSelectedNodePositions.Clear();
						foreach (LineNode ln in m_selectedLineNodes)
							{
							previousSelectedNodePositions.Add(ln.position);
							}

						selectedLineNodes.Clear();
						foreach (LineNode ln in nodesToAdd)
							{
							nodeSteuerung.AddLineNode(ln);
							selectedLineNodes.Add(ln);
							}
						howToDrag = DragNDrop.CREATE_NODE;
						}
					#endregion

					#region Nodes Verbinden
					else if (((Control.ModifierKeys & Keys.Alt) == Keys.Alt) && (selectedLineNodes != null))
						{
						LineNode nodeToConnectTo = nodeSteuerung.GetLineNodeAt(clickedPosition);

						if (nodeToConnectTo != null)
							{
							foreach (LineNode ln in selectedLineNodes)
								{
								nodeSteuerung.Connect(
									ln, 
									nodeToConnectTo, 
									(int)nodeConnectionPrioritySpinEdit.Value,
									(double)spinTargetVelocity.Value,
									carsAllowedCheckBox.Checked, 
									busAllowedCheckBox.Checked, 
									tramAllowedCheckBox.Checked, 
									enableIncomingLineChangeCheckBox.Checked,
									enableOutgoingLineChangeCheckBox.Checked);
								}						
							}
						}
					#endregion

					#region Nodes selektieren bzw. verschieben
					else
						{
						bool found = false;

						if (!lockNodesCheckBox.Checked)
							{
							// erst gucken, ob evtl. In/OutSlopes angeklickt wurden
							if (selectedLineNodes != null && selectedLineNodes.Count >= 1)
								{
									if (m_selectedLineNodes[0].inSlopeRect.Contains(clickedPosition))
										{
										originalSlopes.Clear();
										foreach (LineNode ln in m_selectedLineNodes)
											{
											originalSlopes.Add(ln.inSlope);
											}
										
										howToDrag = DragNDrop.MOVE_IN_SLOPE;
										found = true;
										}
									if (m_selectedLineNodes[0].outSlopeRect.Contains(clickedPosition))
										{
										originalSlopes.Clear();
										foreach (LineNode ln in m_selectedLineNodes)
											{
											originalSlopes.Add(ln.outSlope);
											}

										howToDrag = DragNDrop.MOVE_OUT_SLOPE;
										found = true;
										}
								}
							}

						if (! found)
							{
							LineNode ln = nodeSteuerung.GetLineNodeAt(clickedPosition);
							if (ln != null && !lockNodesCheckBox.Checked)
								{
								if (selectedLineNodes.Contains(ln))
									{
									// MovingOffsets berechnen:
									selectedLineNodesMovingOffset.Clear();
									foreach (LineNode lln in m_selectedLineNodes)
										{
										selectedLineNodesMovingOffset.Add(lln.position - clickedPosition);
										}

									howToDrag = DragNDrop.MOVE_NODES;
									}
								else
									{
									// bei nur einem Punkt brauchen wir keine MovingOffsets
									selectedLineNodesMovingOffset.Clear();
									selectedLineNodesMovingOffset.Add(new Vector2(0, 0));

									// Häßlicher Workaround, um Settermethode für selectedLineNodes aufzurufen
									List<LineNode> foo = new List<LineNode>();
 									foo.Add(ln);
									selectedLineNodes = foo;

									howToDrag = DragNDrop.MOVE_NODES;
									}
								}
							else
								{
								howToDrag = DragNDrop.DRAG_RUBBERBAND;

								daGridRubberband.Location = clickedPosition;
								daGridRubberband.Width = 1;
								daGridRubberband.Height = 1;
								}
							}
						}
					#endregion
					break;
				case MouseButtons.Right:
					if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
						{
						#region Nodes löschen
						this.Cursor = Cursors.Default;
						// LineNode entfernen
						LineNode nodeToDelete = nodeSteuerung.GetLineNodeAt(clickedPosition);
						// checken ob gefunden
						if (nodeToDelete != null)
							{
							if (selectedLineNodes.Contains(nodeToDelete))
								{
								selectedLineNodes.Remove(nodeToDelete);
								}
							nodeSteuerung.DeleteLineNode(nodeToDelete);
							}
						#endregion
						}
					else
						{
						#region move main grid
						howToDrag = DragNDrop.MOVE_MAIN_GRID;
						daGridRubberband.Location = clickedPosition;
						this.Cursor = Cursors.SizeAll;
						#endregion
						}

					break;
				case MouseButtons.XButton1:
					daGridScrollPosition.Y += 10 * e.Delta;
					UpdateDaGridClippingRect();
					DaGrid.Invalidate();
					thumbGrid.Invalidate();
					break;
				}

			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		void DaGrid_MouseMove(object sender, MouseEventArgs e)
			{
			Vector2 clickedPosition = new Vector2(e.X, e.Y);
			clickedPosition *= zoomMultipliers[zoomComboBox.SelectedIndex, 1];
			clickedPosition += daGridScrollPosition;
			lblMouseCoordinates.Text = "Current Mouse Coordinates (m): " + (clickedPosition * 0.1).ToString();

			this.Cursor = (howToDrag == DragNDrop.MOVE_MAIN_GRID) ? Cursors.SizeAll : Cursors.Default;

			if (selectedLineNodes != null)
				{
				switch (howToDrag)
					{
					case DragNDrop.MOVE_MAIN_GRID:
						clickedPosition = new Vector2(e.X, e.Y);
						clickedPosition *= zoomMultipliers[zoomComboBox.SelectedIndex, 1];
						daGridScrollPosition = new Point((int)Math.Round(-clickedPosition.X + daGridRubberband.X), (int)Math.Round(-clickedPosition.Y + daGridRubberband.Y));
						UpdateDaGridClippingRect();
						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						break;
					case DragNDrop.MOVE_NODES:
						for (int i = 0; i < m_selectedLineNodes.Count; i++)
							{
							m_selectedLineNodes[i].position = DaGrid.DockToGrid(clickedPosition + selectedLineNodesMovingOffset[i], dockToGrid);
							}

						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						break;
					case DragNDrop.CREATE_NODE:
						selectedLineNodes[0].outSlopeAbs = DaGrid.DockToGrid(clickedPosition, dockToGrid);
						selectedLineNodes[0].inSlope = -1 * selectedLineNodes[0].outSlope;

						for (int i = 1; i < m_selectedLineNodes.Count; i++)
							{
							// Rotation des offsets im Vergleich zum outSlope berechnen
							double rotation = Math.Atan2(selectedLineNodesMovingOffset[i].Y, selectedLineNodesMovingOffset[i].X) - Math.Atan2(selectedLineNodesMovingOffset[0].Y, selectedLineNodesMovingOffset[0].X);

							m_selectedLineNodes[i].position = m_selectedLineNodes[0].position - (m_selectedLineNodes[0].outSlope.RotateCounterClockwise(rotation).Normalized * selectedLineNodesMovingOffset[i].Abs);

							double streckungsfaktor = Math.Pow((m_selectedLineNodes[i].position - previousSelectedNodePositions[i]).Abs / (m_selectedLineNodes[0].position - previousSelectedNodePositions[0]).Abs, 2);

							m_selectedLineNodes[i].outSlope = m_selectedLineNodes[0].outSlope * streckungsfaktor;
							m_selectedLineNodes[i].inSlope = m_selectedLineNodes[0].inSlope * streckungsfaktor;
							}

						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						break;
					case DragNDrop.MOVE_IN_SLOPE:
						selectedLineNodes[0].inSlopeAbs = DaGrid.DockToGrid(clickedPosition, dockToGrid);

						// sind mehr als ein LineNode markiert, so sollen die inSlopes aller anderen LineNodes angepasst werden.
						if (m_selectedLineNodes.Count > 1)
							{
							// zur relativen Anpassung eignen sich Polarkoordinaten besonders gut, wir berechnen zunächst die Änderungen
							double streckungsfaktor = m_selectedLineNodes[0].inSlope.Abs / originalSlopes[0].Abs;
							double rotation = Math.Atan2(m_selectedLineNodes[0].inSlope.Y, m_selectedLineNodes[0].inSlope.X) - Math.Atan2(originalSlopes[0].Y, originalSlopes[0].X);

							for (int i = 0; i < m_selectedLineNodes.Count; i++)
								{
								if (i > 0)
									{
									m_selectedLineNodes[i].inSlope = originalSlopes[i].RotateCounterClockwise(rotation);
									m_selectedLineNodes[i].inSlope *= streckungsfaktor;
									}
								}
							}


						if (!((Control.ModifierKeys & Keys.Alt) == Keys.Alt))
							{
							foreach (LineNode ln in m_selectedLineNodes)
								{
								ln.outSlope = -1 * ln.inSlope;
								}
							}
						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						break;
					case DragNDrop.MOVE_OUT_SLOPE:
						selectedLineNodes[0].outSlopeAbs = DaGrid.DockToGrid(clickedPosition, dockToGrid);

						// sind mehr als ein LineNode markiert, so sollen die outSlopes aller anderen LineNodes angepasst werden.
						if (m_selectedLineNodes.Count > 1)
							{
							// zur relativen Anpassung eignen sich Polarkoordinaten besonders gut, wir berechnen zunächst die Änderungen
							double streckungsfaktor = m_selectedLineNodes[0].outSlope.Abs / originalSlopes[0].Abs;
							double rotation = Math.Atan2(m_selectedLineNodes[0].outSlope.Y, m_selectedLineNodes[0].outSlope.X) - Math.Atan2(originalSlopes[0].Y, originalSlopes[0].X);

							for (int i = 0; i < m_selectedLineNodes.Count; i++)
								{
								if (i > 0)
									{
									m_selectedLineNodes[i].outSlope = originalSlopes[i].RotateCounterClockwise(rotation);
									m_selectedLineNodes[i].outSlope *= streckungsfaktor;
									}
								}
							}

						if (!((Control.ModifierKeys & Keys.Alt) == Keys.Alt))
							{
							foreach (LineNode ln in m_selectedLineNodes)
								{
								ln.inSlope = -1 * ln.outSlope;
								}							
							}
						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						break;
					case DragNDrop.DRAG_RUBBERBAND:
						daGridRubberband.Width = (int) Math.Round(clickedPosition.X - daGridRubberband.X);
						daGridRubberband.Height = (int) Math.Round(clickedPosition.Y - daGridRubberband.Y);
						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						break;
					default:
						break;
					}
				}
			else
				{
				}
			}

		void DaGrid_MouseUp(object sender, MouseEventArgs e)
			{
			Vector2 clickedPosition = new Vector2(e.X, e.Y);
			clickedPosition *= zoomMultipliers[zoomComboBox.SelectedIndex, 1];
			clickedPosition += daGridScrollPosition;
			this.Cursor = Cursors.Default;

			switch (howToDrag)
				{
				case DragNDrop.DRAG_RUBBERBAND:
					// nun nochLineNode markieren
					if (Math.Abs(daGridRubberband.Width) > 2 && Math.Abs(daGridRubberband.Height) > 2)
						{
						// Rubberband normalisieren:
						if (daGridRubberband.Width < 0)
							{
							daGridRubberband.X += daGridRubberband.Width;
							daGridRubberband.Width *= -1;
							}
						if (daGridRubberband.Height < 0)
							{
							daGridRubberband.Y += daGridRubberband.Height;
							daGridRubberband.Height *= -1;
							}

						if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
							{
							List<LineNode> tmp = nodeSteuerung.GetLineNodesAt(daGridRubberband);
							foreach (LineNode ln in selectedLineNodes)
								{
								if (!tmp.Contains(ln))
									tmp.Add(ln);
								}
							selectedLineNodes = tmp;
							}
						else
							{
							selectedLineNodes = nodeSteuerung.GetLineNodesAt(daGridRubberband);
							}
						}
					else 
						{
						selectedLineNodes = new List<LineNode>();
						selectedNodeConnection = nodeSteuerung.GetNodeConnectionAt(clickedPosition);
						selectedVehicle = nodeSteuerung.GetVehicleAt(clickedPosition);
						}
					break;
				case DragNDrop.MOVE_MAIN_GRID:
					thumbGrid.Invalidate();
					break;
				default:
					break;
				}

			if ((howToDrag == DragNDrop.CREATE_NODE || howToDrag == DragNDrop.MOVE_NODES || howToDrag == DragNDrop.MOVE_IN_SLOPE || howToDrag == DragNDrop.MOVE_OUT_SLOPE) && m_selectedLineNodes != null)
				{
				nodeSteuerung.UpdateNodeConnections(m_selectedLineNodes);
				nodeSteuerung.InvalidateNodeBounds();
				UpdateDaGridClippingRect();
				thumbGrid.Invalidate();
				}

			// Drag'n'Drop Bereich wieder löschen
			howToDrag = DragNDrop.NONE;
			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		void DaGrid_MouseWheel(object sender, MouseEventArgs e)
			{
			if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
				zoomComboBox.SelectedIndex = Math2.Clamp(zoomComboBox.SelectedIndex + (e.Delta / 120), 0, zoomComboBox.Items.Count - 1);
			}

		void DaGrid_KeyDown(object sender, KeyEventArgs e)
			{
			// Tastenbehandlung
			switch (e.KeyCode)
				{
			#region Nodes verschieben
			// Node verschieben
			case Keys.Left:
				foreach (LineNode ln in selectedLineNodes)
					{
					ln.position.X -= 1;
					e.Handled = true;
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				break;
			case Keys.Right:
				foreach (LineNode ln in selectedLineNodes)
					{
					ln.position.X += 1;
					e.Handled = true;
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				break;
			case Keys.Up:
				foreach (LineNode ln in selectedLineNodes)
					{
					ln.position.Y -= 1;
					e.Handled = true;
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				break;
			case Keys.Down:
				foreach (LineNode ln in selectedLineNodes)
					{
					ln.position.Y += 1;
					e.Handled = true;
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				break;
			#endregion

			#region Nodes durchwandern
			// TODO: nächster Node
			case Keys.PageDown:
				break;
			// TODO: vorheriger Node
			case Keys.PageUp:
				break;
			#endregion

			#region Nodes bearbeiten
			// Node löschen
			case Keys.Delete:
				if (selectedVehicle != null)
					{
					selectedVehicle.currentNodeConnection.RemoveVehicle(selectedVehicle);
					}
				else // do not delete nodes and connections when vehicle selected!
					{
					foreach (LineNode ln in selectedLineNodes)
						{
						nodeSteuerung.DeleteLineNode(ln);
						}
					selectedLineNodes.Clear();
					e.Handled = true;
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);

					if (selectedNodeConnection != null)
						{
						nodeSteuerung.Disconnect(selectedNodeConnection.startNode, selectedNodeConnection.endNode);
						selectedNodeConnection = null;
						e.Handled = true;
						Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
						}
					}
				break;

			// LineSegment teilen
			case Keys.S:
				if (selectedNodeConnection != null)
					{
					nodeSteuerung.SplitNodeConnection(selectedNodeConnection);
					selectedNodeConnection = null;
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				break;
			case Keys.Return:
				if (selectedNodeConnection != null)
					{
					nodeSteuerung.RemoveLineChangePoints(selectedNodeConnection, true, false);
					nodeSteuerung.FindLineChangePoints(selectedNodeConnection, Constants.maxDistanceToLineChangePoint, Constants.maxDistanceToParallelConnection);
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				break;

			case Keys.C:
				if (selectedNodeConnection != null)
					{
					carsAllowedCheckBox.Checked = !carsAllowedCheckBox.Checked;
					}
				break;
			case Keys.B:
				if (selectedNodeConnection != null)
					{
					busAllowedCheckBox.Checked = !busAllowedCheckBox.Checked;
					}
				break;
			case Keys.T:
				if (selectedNodeConnection != null)
					{
					tramAllowedCheckBox.Checked = !tramAllowedCheckBox.Checked;
					}
				break;

			case Keys.O:
				if (selectedNodeConnection != null)
					{
					enableOutgoingLineChangeCheckBox.Checked = !enableOutgoingLineChangeCheckBox.Checked;
					enableOutgoingLineChangeCheckBox_Click(this, new EventArgs());
					}
				break;
			case Keys.I:
				if (selectedNodeConnection != null)
					{
					enableIncomingLineChangeCheckBox.Checked = !enableIncomingLineChangeCheckBox.Checked;
					enableIncomingLineChangeCheckBox_Click(this, new EventArgs());
					}
				break;

			// reset LineNode slopes
			case Keys.R:
				foreach (LineNode ln in selectedLineNodes)
					{
					if (!ln.outSlope.IsZeroVector() && !ln.inSlope.IsZeroVector())
						{
						ln.outSlope = ln.outSlope.Normalized * 32;
						ln.inSlope = ln.inSlope.Normalized * 32;
						}
					}
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				break;
			#endregion

			#region from-/toLineNodes setzen

			case Keys.V:
				fromLineNodes.Clear();
				foreach (LineNode ln in selectedLineNodes)
					{
					fromLineNodes.Add(ln);
					}
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				break;

			case Keys.N:
				toLineNodes.Clear();
				foreach (LineNode ln in selectedLineNodes)
					{
					toLineNodes.Add(ln);
					}
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				break;
			#endregion

			#region Zoomfaktor ändern
			case Keys.P:
				if (selectedNodeConnection != null)
					{
					nodeConnectionPrioritySpinEdit.Value++;
					break;
					}
				if (((Control.ModifierKeys & Keys.Control) == Keys.Control) && zoomComboBox.SelectedIndex < zoomComboBox.Items.Count - 1)
					{
					zoomComboBox.SelectedIndex += 1;
					}
				break;

			case Keys.M:
				if (selectedNodeConnection != null)
					{
					nodeConnectionPrioritySpinEdit.Value--;
					break;
					}
				if (((Control.ModifierKeys & Keys.Control) == Keys.Control) && zoomComboBox.SelectedIndex > 0)
					{
					zoomComboBox.SelectedIndex -= 1;
					}
				break;

			case Keys.Add:
				if (selectedNodeConnection != null)
					{
					nodeConnectionPrioritySpinEdit.Value++;
					break;
					}
				if (((Control.ModifierKeys & Keys.Control) == Keys.Control) && zoomComboBox.SelectedIndex < zoomComboBox.Items.Count - 1)
					{
					zoomComboBox.SelectedIndex += 1;
					}
				break;

			case Keys.Subtract:
				if (selectedNodeConnection != null)
					{
					nodeConnectionPrioritySpinEdit.Value--;
					break;
					}
				if (((Control.ModifierKeys & Keys.Control) == Keys.Control) && zoomComboBox.SelectedIndex > 0)
					{
					zoomComboBox.SelectedIndex -= 1;
					}
				break;

			#endregion


			#region Connections bearbeiten

			#endregion
			case Keys.D:
				
				break;
				}
			}
		#endregion
		#endregion

		#region Zeichnen
		void DaGrid_Paint(object sender, PaintEventArgs e)
			{
			// TODO: Paint Methode entschlacken und outsourcen?
			switch (renderQualityComboBox.SelectedIndex)
				{
				case 0:
					e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
					e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					break;
				case 1:
					e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
					e.Graphics.InterpolationMode = InterpolationMode.Low;
					break;
				}

			renderStopwatch.Reset();
			renderStopwatch.Start();

			if (resampledBackgroundImage != null)
				{
				resampledBackgroundImage.SetResolution(e.Graphics.DpiX, e.Graphics.DpiY);
				e.Graphics.DrawImageUnscaled(resampledBackgroundImage, new Point((int)Math.Round(-daGridScrollPosition.X * zoomMultipliers[zoomComboBox.SelectedIndex, 0]), (int)Math.Round(-daGridScrollPosition.Y * zoomMultipliers[zoomComboBox.SelectedIndex, 0])));
				}

			if (satelliteImages != null)
				{
				int x, y = 0;
				for (int i = 0; i < satelliteImages.GetLength(0); ++i)
					{
					x = 0;
					for (int j = 0; j < satelliteImages.GetLength(1); ++j)
						{
						if (satelliteImages[i, j] != null)
							{
							e.Graphics.DrawImage(satelliteImages[i, j], new Point(x, y));
							x += satelliteImages[i, j].Width;
							}
						}
					if (satelliteImages[i, 0] != null)
						y += satelliteImages[i, 0].Height;
					}
				}

			if (cbRenderGrid.Checked)
				{
				using (Pen GrayPen = new Pen(Color.LightGray, 1.0f))
					{
					int countX = (int)Math.Ceiling(DaGrid.ClientSize.Width * zoomMultipliers[zoomComboBox.SelectedIndex, 1] / ((float)spinGridSpacing.Value * 10.0f));
					int countY = (int)Math.Ceiling(DaGrid.ClientSize.Height * zoomMultipliers[zoomComboBox.SelectedIndex, 1] / ((float)spinGridSpacing.Value * 10.0f));

					for (int i = 0; i < countX; i++)
						{
						e.Graphics.DrawLine(GrayPen, i * (float)spinGridSpacing.Value * 10.0f * zoomMultipliers[zoomComboBox.SelectedIndex, 0], 0, i * (float)spinGridSpacing.Value * 10.0f * zoomMultipliers[zoomComboBox.SelectedIndex, 0], DaGrid.ClientSize.Height);
						}
					for (int i = 0; i < countY; i++)
						{
						e.Graphics.DrawLine(GrayPen, 0, i * (float)spinGridSpacing.Value * 10.0f * zoomMultipliers[zoomComboBox.SelectedIndex, 0], DaGrid.ClientSize.Width, i * (float)spinGridSpacing.Value * 10.0f * zoomMultipliers[zoomComboBox.SelectedIndex, 0]);
						}
					}
				}

			/*if (_connectionsRenderCache != null)
				{
				e.Graphics.FillRectangle(_connectionsRenderCache, 0, 0, DaGrid.ClientSize.Width, DaGrid.ClientSize.Height);
				}*/
			/*if (_connectionsRenderCacheBmp != null)
				{
				_connectionsRenderCacheBmp.SetResolution(e.Graphics.DpiX, e.Graphics.DpiY);
				e.Graphics.DrawImageUnscaled(_connectionsRenderCacheBmp, 0, 0, DaGrid.ClientSize.Width, DaGrid.ClientSize.Height);
				}*/

			e.Graphics.Transform = new Matrix(
				zoomMultipliers[zoomComboBox.SelectedIndex, 0], 0, 
				0, zoomMultipliers[zoomComboBox.SelectedIndex, 0],
				-daGridScrollPosition.X * zoomMultipliers[zoomComboBox.SelectedIndex, 0], -daGridScrollPosition.Y * zoomMultipliers[zoomComboBox.SelectedIndex, 0]);


			using (Pen BlackPen = new Pen(Color.Black, 1.0F))
				{
				// Zusätzliche Grafiken zeichnen
				foreach (GraphicsPath gp in additionalGraphics)
					{
					e.Graphics.DrawPath(BlackPen, gp);
					}

				nodeSteuerung.RenderNetwork(e.Graphics, renderOptionsDaGrid);

				//to-/fromLineNode malen
				foreach (LineNode ln in toLineNodes)
					{
					RectangleF foo = ln.positionRect;
					foo.Inflate(4, 4);
					e.Graphics.FillEllipse(new SolidBrush(Color.Red), foo);
					}
				foreach (LineNode ln in fromLineNodes)
					{
					RectangleF foo = ln.positionRect;
					foo.Inflate(4, 4);
					e.Graphics.FillEllipse(new SolidBrush(Color.Green), foo);
					}
	
				if (selectedLineNodes.Count >= 1)
					{
					if (cbRenderLineNodes.Checked)
						{
						foreach (LineNode ln in selectedLineNodes)
							{
							foreach (GraphicsPath gp in ln.nodeGraphics)
								{
								e.Graphics.DrawPath(BlackPen, gp);
								}

							RectangleF foo = ln.positionRect;
							foo.Inflate(2, 2);
							e.Graphics.FillEllipse(new SolidBrush(Color.Black), foo);
							}

						RectangleF foo2 = m_selectedLineNodes[0].positionRect;
						foo2.Inflate(4, 4);
						e.Graphics.DrawEllipse(BlackPen, foo2);
						}

					List<Vector2> points = new List<Vector2>(selectedLineNodes.Count);
					foreach (LineNode ln in selectedLineNodes)
						{
						points.Add(ln.position);
						}
					// build convex hull
					GraphicsPath hullPath = AlgorithmicGeometry.roundedConvexHullPath(points, 16);

					using (SolidBrush tmp = new SolidBrush(Color.FromArgb(64, Color.Gold)))
						e.Graphics.FillPath(tmp, hullPath);
					using (Pen tmp = new Pen(Color.Gold, 3))
						e.Graphics.DrawPath(tmp, hullPath);
					}

				if (trafficLightForm.selectedEntry != null)
					{
					List<Pair<SpecificIntersection, double>> foo = trafficLightForm.selectedEntry.parentGroup.GetConflictPoints(trafficLightForm.selectedEntry);
					if (foo != null)
						{
						using (Pen bar = new Pen(Color.Blue, 2))
							{
							foreach (Pair<SpecificIntersection, double> p in foo)
								{
								List<Vector2> l = new List<Vector2>();
								l.Add(p.Left.intersection.aPosition);
								e.Graphics.DrawPath(bar, AlgorithmicGeometry.roundedConvexHullPath(l, 8));
								}
							}
						}
					}

				// selektierte NodeConnection malen
				if (selectedNodeConnection != null)
					{
					selectedNodeConnection.lineSegment.Draw(e.Graphics, BlackPen);
					}

				if (selectedVehicle != null && renderOptionsDaGrid.renderVehicleDebugData)
					{
					Pen prevNodeConnectionsPen = new Pen(Color.Red, 3);
					Pen nextNodeConnectionsPen = new Pen(Color.Green, 3);

					foreach (NodeConnection prevNC in selectedVehicle.visitedNodeConnections)
						{
						e.Graphics.DrawBezier(prevNodeConnectionsPen, prevNC.lineSegment.p0, prevNC.lineSegment.p1, prevNC.lineSegment.p2, prevNC.lineSegment.p3);
						}
					foreach (Routing.RouteSegment rs in selectedVehicle.wayToGo)
						{
						if (!rs.lineChangeNeeded)
							{
							NodeConnection nextNC = rs.startConnection;
							e.Graphics.DrawBezier(nextNodeConnectionsPen, nextNC.lineSegment.p0, nextNC.lineSegment.p1, nextNC.lineSegment.p2, nextNC.lineSegment.p3);
							}
						else
							{
							e.Graphics.DrawLine(nextNodeConnectionsPen, rs.startConnection.startNode.position, rs.nextNode.position);
							}
						}
					}


				// Gummiband zeichnen
				if (howToDrag == DragNDrop.DRAG_RUBBERBAND)
					{
					Point[] points = 
						{
							new Point(daGridRubberband.X, daGridRubberband.Y),
							new Point(daGridRubberband.X + daGridRubberband.Width, daGridRubberband.Y),
							new Point(daGridRubberband.X + daGridRubberband.Width, daGridRubberband.Y + daGridRubberband.Height),
							new Point(daGridRubberband.X, daGridRubberband.Y + daGridRubberband.Height)
						};
					using (SolidBrush tmp = new SolidBrush(Color.FromArgb(32, Color.Black)))
						e.Graphics.FillPolygon(tmp, points);
					e.Graphics.DrawPolygon(BlackPen, points);
					}

				additionalGraphics.Clear();


				// Statusinfo zeichnen:
				if (selectedVehicle != null && cbRenderVehiclesDebug.Checked)
					{
					selectedVehicle.DrawDebugData(e.Graphics);
					}

				renderStopwatch.Stop();


				if (cbRenderFps.Checked)
					{
					e.Graphics.Transform = new Matrix(1, 0, 0, 1, 0, 0);
					e.Graphics.DrawString(
						"thinking time: " + thinkStopwatch.ElapsedMilliseconds + "ms, possible thoughts per second: " + ((thinkStopwatch.ElapsedMilliseconds != 0) ? (1000 / thinkStopwatch.ElapsedMilliseconds).ToString() : "-"),
						new Font("Arial", 10),
						new SolidBrush(Color.Black),
						8,
						40);

					e.Graphics.DrawString(
						"rendering time: " + renderStopwatch.ElapsedMilliseconds + "ms, possible fps: " + ((renderStopwatch.ElapsedMilliseconds != 0) ? (1000 / renderStopwatch.ElapsedMilliseconds).ToString() : "-"),
						new Font("Arial", 10),
						new SolidBrush(Color.Black),
						8,
						56);
					}
				}
			}

		private void thumbGrid_Paint(object sender, PaintEventArgs e)
			{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.InterpolationMode = InterpolationMode.Bilinear;

			// Zoomfaktor berechnen
			RectangleF bounds = nodeSteuerung.GetLineNodeBounds();
			float zoom = Math.Min(1.0f, Math.Min((float)thumbGrid.ClientSize.Width / bounds.Width, (float)thumbGrid.ClientSize.Height / bounds.Height));

			e.Graphics.Transform = new Matrix(zoom, 0, 0, zoom, -bounds.X * zoom, -bounds.Y * zoom);


			using (Pen BlackPen = new Pen(Color.Black, 1.0F))
				{
				nodeSteuerung.RenderNetwork(e.Graphics, renderOptionsThumbnail);

				if (fromLineNodes.Count > 0 && toLineNodes.Count > 0)
					{
					Routing route = Routing.CalculateShortestConenction(fromLineNodes[0], toLineNodes, Vehicle.IVehicle.VehicleTypes.CAR);

					using (Pen orangePen = new Pen(Color.Orange, 4 / zoom))
						{
						foreach (Routing.RouteSegment rs in route)
							{
							if (!rs.lineChangeNeeded)
								{
								NodeConnection nextNC = rs.startConnection;
								e.Graphics.DrawBezier(orangePen, nextNC.lineSegment.p0, nextNC.lineSegment.p1, nextNC.lineSegment.p2, nextNC.lineSegment.p3);
								}
							else
								{
								e.Graphics.DrawLine(orangePen, rs.startConnection.startNode.position, rs.nextNode.position);
								}
							}
						}

					}

				//to-/fromLineNode malen
				foreach (LineNode ln in toLineNodes)
					{
					RectangleF foo = ln.positionRect;
					foo.Inflate(4 / zoom, 4 / zoom);
					e.Graphics.FillEllipse(new SolidBrush(Color.Red), foo);
					}
				foreach (LineNode ln in fromLineNodes)
					{
					RectangleF foo = ln.positionRect;
					foo.Inflate(4 / zoom, 4 / zoom);
					e.Graphics.FillEllipse(new SolidBrush(Color.Green), foo);
					}

				e.Graphics.Transform = new Matrix(1, 0, 0, 1, 0, 0);
				e.Graphics.DrawRectangle(BlackPen, thumbGridClientRect);
				}

			}

		/// <summary>
		/// aktualisiert das Clipping-Rectangle von DaGrid
		/// </summary>
		private void UpdateDaGridClippingRect()
			{
			if (zoomComboBox.SelectedIndex >= 0)
				{
				// daGridClippingRect aktualisieren
				renderOptionsDaGrid.clippingRect.X = daGridScrollPosition.X;
				renderOptionsDaGrid.clippingRect.Y = daGridScrollPosition.Y;
				renderOptionsDaGrid.clippingRect.Width = (int)Math.Ceiling(pnlMainGrid.ClientSize.Width * zoomMultipliers[zoomComboBox.SelectedIndex, 1]);
				renderOptionsDaGrid.clippingRect.Height = (int)Math.Ceiling(pnlMainGrid.ClientSize.Height * zoomMultipliers[zoomComboBox.SelectedIndex, 1]);

				daGridViewCenter = new PointF(
					daGridScrollPosition.X + (pnlMainGrid.ClientSize.Width / 2 * zoomMultipliers[zoomComboBox.SelectedIndex, 1]),
					daGridScrollPosition.Y + (pnlMainGrid.ClientSize.Height / 2 * zoomMultipliers[zoomComboBox.SelectedIndex, 1]));

				RectangleF bounds = nodeSteuerung.GetLineNodeBounds();
				float zoom = Math.Min(1.0f, Math.Min((float)thumbGrid.ClientSize.Width / bounds.Width, (float)thumbGrid.ClientSize.Height / bounds.Height));

				thumbGridClientRect = new Rectangle(
					(int)Math.Round((daGridScrollPosition.X - bounds.X) * zoom),
					(int)Math.Round((daGridScrollPosition.Y - bounds.Y) * zoom),
					(int)Math.Round(pnlMainGrid.ClientSize.Width * zoomMultipliers[zoomComboBox.SelectedIndex, 1] * zoom),
					(int)Math.Round(pnlMainGrid.ClientSize.Height * zoomMultipliers[zoomComboBox.SelectedIndex, 1] * zoom));

				lblScrollPosition.Text = "Canvas Location (dm): (" + daGridScrollPosition.X + ", " + daGridScrollPosition.Y + ") -> (" + (daGridScrollPosition.X + renderOptionsDaGrid.clippingRect.Width) + ", " + (daGridScrollPosition.Y + renderOptionsDaGrid.clippingRect.Height) + ")";

				UpdateConnectionsRenderCache();
				}
			}

		#endregion


		#region Eventhandler

		#region Speichern/Laden
		private void SpeichernButton_Click(object sender, EventArgs e)
			{
			using (SaveFileDialog sfd = new SaveFileDialog())
				{
				sfd.InitialDirectory = Application.ExecutablePath;
				sfd.AddExtension = true;
				sfd.DefaultExt = @".xml";
				sfd.Filter = @"XML Dateien|*.xml";

				if (sfd.ShowDialog() == DialogResult.OK)
					{
					Tools.ProgramSettings ps = new Tools.ProgramSettings();
					ps._simSpeed = simulationSpeedSpinEdit.Value;
					ps._simSteps = stepsPerSecondSpinEdit.Value;
					ps._simDuration = spinSimulationDuration.Value;
					ps._simRandomSeed = spinRandomSeed.Value;
					ps._zoomLevel = zoomComboBox.SelectedIndex;
					ps._renderQuality = renderQualityComboBox.SelectedIndex;
					ps._renderStatistics = cbRenderStatistics.Checked;
					ps._renderVelocityMapping = cbVehicleVelocityMapping.Checked;
					ps._showFPS = cbRenderFps.Checked;
					ps._renderOptions = renderOptionsDaGrid;
					ps._velocityMappingColorMap = cmcVelocityMapping.colormap;

					XmlSaver.SaveToFile(sfd.FileName, nodeSteuerung, timelineSteuerung, trafficVolumeSteuerung, ps);
					}
				}
			}

		private void LadenButton_Click(object sender, EventArgs e)
			{
			using (OpenFileDialog ofd = new OpenFileDialog())
				{
				ofd.InitialDirectory = Application.ExecutablePath;
				ofd.AddExtension = true;
				ofd.DefaultExt = @".xml";
				ofd.Filter = @"XML Dateien|*.xml";

				if (ofd.ShowDialog() == DialogResult.OK)
					{
					// erstma alles vorhandene löschen
					selectedLineNodes.Clear();
					timelineSteuerung.Clear();

					// Laden
					Tools.ProgramSettings ps = XmlSaver.LoadFromFile(ofd.FileName, nodeSteuerung, timelineSteuerung, trafficVolumeSteuerung);

					titleEdit.Text = nodeSteuerung.title;
					infoEdit.Text = nodeSteuerung.infoText;
					nodeSteuerung.ResetAverageVelocities();

					if (ps._simSpeed >= simulationSpeedSpinEdit.Minimum && ps._simSpeed <= simulationSpeedSpinEdit.Maximum)
						simulationSpeedSpinEdit.Value = ps._simSpeed;
					if (ps._simSteps >= stepsPerSecondSpinEdit.Minimum && ps._simSteps <= stepsPerSecondSpinEdit.Maximum) 
						stepsPerSecondSpinEdit.Value = ps._simSteps;
					if (ps._simDuration >= spinSimulationDuration.Minimum && ps._simDuration <= spinSimulationDuration.Maximum) 
						spinSimulationDuration.Value = ps._simDuration;
					if (ps._simRandomSeed >= spinRandomSeed.Minimum && ps._simRandomSeed <= spinRandomSeed.Maximum) 
						spinRandomSeed.Value = ps._simRandomSeed;
					if (ps._zoomLevel >= 0 && ps._zoomLevel < zoomComboBox.Items.Count)
						zoomComboBox.SelectedIndex = ps._zoomLevel;
					if (ps._renderQuality >= 0 && ps._renderQuality < renderQualityComboBox.Items.Count)
						renderQualityComboBox.SelectedIndex = ps._renderQuality;

					cbRenderStatistics.Checked = ps._renderStatistics;
					cbVehicleVelocityMapping.Checked = ps._renderVelocityMapping;
					cbRenderFps.Checked = ps._showFPS;

					renderOptionsDaGrid = ps._renderOptions;
					cbRenderLineNodes.Checked = ps._renderOptions.renderLineNodes;
					cbRenderConnections.Checked = ps._renderOptions.renderNodeConnections;
					cbRenderVehicles.Checked = ps._renderOptions.renderVehicles;
					daGridScrollPosition = ps._renderOptions.clippingRect.Location;
					cbRenderIntersections.Checked = ps._renderOptions.renderIntersections;
					cbRenderLineChangePoints.Checked = ps._renderOptions.renderLineChangePoints;
					cbRenderLineNodesDebug.Checked = ps._renderOptions.renderLineNodeDebugData;
					cbRenderConnectionsDebug.Checked = ps._renderOptions.renderNodeConnectionDebugData;
					cbRenderVehiclesDebug.Checked = ps._renderOptions.renderVehicleDebugData;

					cmcVelocityMapping.colormap = ps._velocityMappingColorMap;

					// neuzeichnen
					UpdateDaGridClippingRect();
					Invalidate(InvalidationLevel.ALL);
					thumbGrid.Invalidate();
					}
				}
			}
		#endregion


		private void timer1_Tick(object sender, EventArgs e)
			{
			thinkStopwatch.Reset();
			thinkStopwatch.Start();

			double tickLength = 1.0d / (double)stepsPerSecondSpinEdit.Value;

			if (GlobalTime.Instance.currentTime < (double)spinSimulationDuration.Value && (GlobalTime.Instance.currentTime + tickLength) >= (double)spinSimulationDuration.Value)
				cbEnableSimulation.Checked = false;

			timelineSteuerung.Advance(tickLength);
			GlobalTime.Instance.Advance(tickLength);

			//tickCount++;

			nodeSteuerung.Tick(tickLength);
			trafficVolumeSteuerung.Tick(tickLength);
				
			nodeSteuerung.Reset();

			thinkStopwatch.Stop();
			Invalidate(InvalidationLevel.MAIN_CANVAS_AND_TIMLINE);
			}

		#endregion

		private void timerOnCheckBox_CheckedChanged(object sender, EventArgs e)
			{
			timerSimulation.Enabled = cbEnableSimulation.Checked;
			}

		private void BildLadenButton_Click(object sender, EventArgs e)
			{
			using (OpenFileDialog ofd = new OpenFileDialog())
				{
				ofd.InitialDirectory = Application.ExecutablePath;
				ofd.AddExtension = true;
				ofd.DefaultExt = @".*";
				ofd.Filter = @"Bilder|*.*";

				if (ofd.ShowDialog() == DialogResult.OK)
					{
					backgroundImageEdit.Text = ofd.FileName;
					backgroundImage = new Bitmap(backgroundImageEdit.Text);
					UpdateBackgroundImage();
					Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
					}
				}

			}

		private void UpdateBackgroundImage()
			{
			if (backgroundImage != null)
				{
				if (resampledBackgroundImage != null)
					resampledBackgroundImage.Dispose();
				resampledBackgroundImage = null;
				resampledBackgroundImage = ResizeBitmap(
					backgroundImage,
					(int)Math.Round(backgroundImage.Width * zoomMultipliers[zoomComboBox.SelectedIndex, 0] * ((float)backgroundImageScalingSpinEdit.Value / 100)),
					(int)Math.Round(backgroundImage.Height * zoomMultipliers[zoomComboBox.SelectedIndex, 0] * ((float)backgroundImageScalingSpinEdit.Value / 100)));
				}
			}

		private Bitmap ResizeBitmap(Bitmap b, int nWidth, int nHeight)
			{
			Bitmap result = new Bitmap(nWidth, nHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics g = Graphics.FromImage((Image)result))
				{
				g.DrawImage(b, 0, 0, nWidth, nHeight);
				}
			return result;
			}

		private void UpdateConnectionsRenderCache()
			{
			/*int w = Math.Max(1, DaGrid.ClientSize.Width);
			int h = Math.Max(1, DaGrid.ClientSize.Height);
			_connectionsRenderCacheBmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
			using (Graphics g = Graphics.FromImage((Image)_connectionsRenderCacheBmp))
				{
				switch (renderQualityComboBox.SelectedIndex)
					{
					case 0:
						g.SmoothingMode = SmoothingMode.HighQuality;
						g.InterpolationMode = InterpolationMode.HighQualityBicubic;
						break;
					case 1:
						g.SmoothingMode = SmoothingMode.HighSpeed;
						g.InterpolationMode = InterpolationMode.Low;
						break;
					} 
				
				NodeSteuerung.RenderOptions ro = new NodeSteuerung.RenderOptions();
				ro.renderLineNodes = false;
				ro.renderNodeConnections = true;
				ro.renderVehicles = false;
				ro.clippingRect = renderOptionsDaGrid.clippingRect;

				g.Transform = new Matrix(
					zoomMultipliers[zoomComboBox.SelectedIndex, 0], 0,
					0, zoomMultipliers[zoomComboBox.SelectedIndex, 0],
					-daGridScrollPosition.X * zoomMultipliers[zoomComboBox.SelectedIndex, 0], -daGridScrollPosition.Y * zoomMultipliers[zoomComboBox.SelectedIndex, 0]);

				nodeSteuerung.RenderNetwork(g, ro);
				_connectionsRenderCache = new System.Drawing.TextureBrush(_connectionsRenderCacheBmp);
				}*/
			}

		private void Form1_Load(object sender, EventArgs e)
			{
			timelineSteuerung.AddGroup(unsortedGroup);
			}

		/// <summary>
		/// Erweiterung der Invalidate() Methode, die alles neu zeichnet
		/// </summary>
		public void Invalidate(InvalidationLevel il)
			{
			base.Invalidate();
			switch (il)
				{
				case InvalidationLevel.ALL:
					thumbGrid.Invalidate();
					DaGrid.Invalidate();
					break;
				case InvalidationLevel.MAIN_CANVAS_AND_TIMLINE:
					DaGrid.Invalidate();
					break;
				case InvalidationLevel.ONLY_MAIN_CANVAS:
					DaGrid.Invalidate();
					break;
				default:
					break;
				}
			}

		private void nodeConnectionPrioritySpinEdit_ValueChanged(object sender, EventArgs e)
			{
			if (selectedNodeConnection != null)
				{
				selectedNodeConnection.priority = (int)nodeConnectionPrioritySpinEdit.Value;
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				}
			}

		private void stepButton_Click(object sender, EventArgs e)
			{
			timer1_Tick(sender, e);
			Invalidate(InvalidationLevel.MAIN_CANVAS_AND_TIMLINE);
			}

		private void killAllVehiclesButton_Click(object sender, EventArgs e)
			{
			foreach (NodeConnection nc in nodeSteuerung.connections)
				{
				foreach (IVehicle v in nc.vehicles)
					{
					nc.RemoveVehicle(v);
					}

				nc.RemoveAllVehiclesInRemoveList();
				}
			foreach (Intersection i in nodeSteuerung.intersections)
				{
				i.UnregisterAllVehicles();
				}
			}

		private void zoomComboBox_SelectedIndexChanged(object sender, EventArgs e)
			{
			// neue Autoscrollposition berechnen und setzen
			daGridScrollPosition = new Point(
				(int)Math.Round(daGridViewCenter.X - (pnlMainGrid.ClientSize.Width / 2 * zoomMultipliers[zoomComboBox.SelectedIndex, 1])),
				(int)Math.Round(daGridViewCenter.Y - (pnlMainGrid.ClientSize.Height / 2 * zoomMultipliers[zoomComboBox.SelectedIndex, 1])));
			
			// Bitmap umrechnen:
			UpdateBackgroundImage();

			UpdateDaGridClippingRect();
			thumbGrid.Invalidate();
			DaGrid.Invalidate();
			}


		private void clearBackgroudnImageButton_Click(object sender, EventArgs e)
			{
			backgroundImage = null;
			resampledBackgroundImage = null;
			backgroundImageEdit.Text = "";
			DaGrid.Invalidate();
			}

		private void textBox1_Leave(object sender, EventArgs e)
			{
			nodeSteuerung.infoText = infoEdit.Text;
			}

		private void aboutBoxButton_Click(object sender, EventArgs e)
			{
			AboutBox a = new AboutBox();
			a.Show();
			}

		private void thumbGrid_Resize(object sender, EventArgs e)
			{
			UpdateDaGridClippingRect();
			}

		private void carsAllowedCheckBox_CheckedChanged(object sender, EventArgs e)
			{
			if (selectedNodeConnection != null)
				{
				selectedNodeConnection.carsAllowed = carsAllowedCheckBox.Checked;
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				}
			}

		private void busAllowedCheckBox_CheckedChanged(object sender, EventArgs e)
			{
			if (selectedNodeConnection != null)
				{
				selectedNodeConnection.busAllowed = busAllowedCheckBox.Checked;
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				}
			}

		private void tramAllowedCheckBox_CheckedChanged(object sender, EventArgs e)
			{
			if (selectedNodeConnection != null)
				{
				selectedNodeConnection.tramAllowed = tramAllowedCheckBox.Checked;
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				}
			}

		private void findLineChangePointsButton_Click(object sender, EventArgs e)
			{
			foreach (NodeConnection nc in nodeSteuerung.connections)
				{
				if (nc.enableOutgoingLineChange && (nc.carsAllowed || nc.busAllowed))
					{
					nodeSteuerung.RemoveLineChangePoints(nc, true, false);
					nodeSteuerung.FindLineChangePoints(nc, Constants.maxDistanceToLineChangePoint, Constants.maxDistanceToParallelConnection);
					}
				}
			DaGrid.Invalidate();
			}

		private void visualizationCheckBox_CheckedChanged(object sender, EventArgs e)
			{
			nodeSteuerung.setVisualizationInNodeConnections(cbRenderStatistics.Checked);
			DaGrid.Invalidate();
			}

		private void titleEdit_Leave(object sender, EventArgs e)
			{
			nodeSteuerung.title = titleEdit.Text;
			}

		private void titleEdit_TextChanged(object sender, EventArgs e)
			{
			if (titleEdit.Text.Equals(""))
				this.Text = "CityTrafficSimulator";
			else
				this.Text = "CityTrafficSimulator - " + titleEdit.Text;
			}

		private void enableIncomingLineChangeCheckBox_Click(object sender, EventArgs e)
			{
			if (m_selectedNodeConnection != null)
				{
				m_selectedNodeConnection.enableIncomingLineChange = enableIncomingLineChangeCheckBox.Checked;
				if (enableIncomingLineChangeCheckBox.Checked)
					{
					// TODO: zu unperformant - andere Lösung muss her
					/*
					foreach (NodeConnection nc in nodeSteuerung.connections)
						{
						if (nc.enableOutgoingLineChange)
							{
							nodeSteuerung.RemoveLineChangePoints(nc, true, false);
							nodeSteuerung.FindLineChangePoints(nc, Constants.maxDistanceToLineChangePoint, Constants.maxDistanceToParallelConnection);
							}
						}
					 * */
					}
				else
					{
					nodeSteuerung.RemoveLineChangePoints(m_selectedNodeConnection, false, true);
					}
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				}

			}

		private void enableOutgoingLineChangeCheckBox_Click(object sender, EventArgs e)
			{
			if (m_selectedNodeConnection != null)
				{
				m_selectedNodeConnection.enableOutgoingLineChange = enableOutgoingLineChangeCheckBox.Checked;
				if (enableOutgoingLineChangeCheckBox.Checked && (m_selectedNodeConnection.carsAllowed || m_selectedNodeConnection.busAllowed))
					{
					nodeSteuerung.FindLineChangePoints(m_selectedNodeConnection, Constants.maxDistanceToLineChangePoint, Constants.maxDistanceToParallelConnection);
					}
				else
					{
					nodeSteuerung.RemoveLineChangePoints(m_selectedNodeConnection, true, false);
					}
				Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
				}
			}

		private void trafficLightTreeView_AfterSelect(object sender, TreeViewEventArgs e)
			{
			// unendliche Rekursion vermeiden
			if (! doHandleTrafficLightTreeViewSelect)
				return;

			// ausgewähltes TrafficLight bestimmen
			TrafficLight tl = e.Node.Tag as TrafficLight;

			// es wurde ein TrafficLight ausgewählt
			if (tl != null)
				{
				// entweder den ausgewählten Nodes die LSA zuordnen
				if (m_selectedLineNodes.Count > 0)
					{
					foreach (LineNode ln in m_selectedLineNodes)
						{
						if (ln.tLight != null)
							{
							ln.tLight.RemoveAssignedLineNode(ln);
							}

						tl.AddAssignedLineNode(ln);
						}

					trafficLightForm.selectedEntry = tl;
					}
				// oder die der LSA zugeordneten Nodes auswählen
				else
					{
					m_selectedLineNodes.Clear();
					m_selectedLineNodes.AddRange(tl.assignedNodes);
					}
				}
			// es wurde kein TrafficLight ausgewählt
			else
				{
				if (m_selectedLineNodes.Count > 0)
					{
					// dann den ausgewählten LineNodes eine evtl. zugeordnete Ampel wegnehmen
					foreach (LineNode ln in m_selectedLineNodes)
						{
						if (ln.tLight != null)
							{
							ln.tLight.RemoveAssignedLineNode(ln);
							}
						}

					trafficLightForm.selectedEntry = null;
					}
				}

			// neu zeichnen lohnt sich immer
			Invalidate(InvalidationLevel.MAIN_CANVAS_AND_TIMLINE);
			}

		private void backgroundImageScalingSpinEdit_ValueChanged(object sender, EventArgs e)
			{
			UpdateBackgroundImage();
			DaGrid.Invalidate();
			}

		private void simulationSpeedSpinEdit_ValueChanged(object sender, EventArgs e)
			{
			timerSimulation.Interval = (int)(1000 / stepsPerSecondSpinEdit.Value / simulationSpeedSpinEdit.Value);
			}

		private void stepsPerSecondSpinEdit_ValueChanged(object sender, EventArgs e)
			{
			timerSimulation.Interval = (int)(1000 / stepsPerSecondSpinEdit.Value / simulationSpeedSpinEdit.Value);
			UpdateSimulationParameters();
			}

		private void freeNodeButton_Click(object sender, EventArgs e)
			{
			if (m_selectedLineNodes.Count > 0)
				{
				// dann den ausgewählten LineNodes eine evtl. zugeordnete Ampel wegnehmen
				foreach (LineNode ln in m_selectedLineNodes)
					{
					if (ln.tLight != null)
						{
						ln.tLight.RemoveAssignedLineNode(ln);
						}
					}

				trafficLightForm.selectedEntry = null;
				}

			Invalidate(InvalidationLevel.MAIN_CANVAS_AND_TIMLINE);
			}

		private void cbRenderLineNodes_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderLineNodes = cbRenderLineNodes.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderConnections_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderNodeConnections = cbRenderConnections.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderVehicles_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderVehicles = cbRenderVehicles.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderLineNodesDebug_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderLineNodeDebugData = cbRenderLineNodesDebug.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderConnectionsDebug_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderNodeConnectionDebugData = cbRenderConnectionsDebug.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderVehiclesDebug_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderVehicleDebugData = cbRenderVehiclesDebug.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderIntersections_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderIntersections = cbRenderIntersections.Checked;
			DaGrid.Invalidate();
			}

		private void cbRenderLineChangePoints_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.renderLineChangePoints = cbRenderLineChangePoints.Checked;
			DaGrid.Invalidate();
			}

		private void pnlNetworkInfo_Resize(object sender, EventArgs e)
			{
			int h = Math.Max(pnlNetworkInfo.ClientSize.Height, 150);
			int w = pnlNetworkInfo.ClientSize.Width;

			titleEdit.Size = new System.Drawing.Size(w - titleEdit.Location.X - 3, titleEdit.Height);
			infoEdit.Size = new System.Drawing.Size(w - 6, h - infoEdit.Location.Y - 61);

			LadenButton.Location = new System.Drawing.Point(w - 85 - 6 - 85 - 3, h - 55);
			SpeichernButton.Location = new System.Drawing.Point(w - 85 - 3, h - 55);
			aboutBoxButton.Location = new System.Drawing.Point(w - 85 - 6 - 85 - 3, h - 26);
			}

		private void spinTargetVelocity_ValueChanged(object sender, EventArgs e)
			{
			if (selectedNodeConnection != null)
				{
				selectedNodeConnection.targetVelocity = (double)spinTargetVelocity.Value;
				}
			}

		private void pnlSignalAssignment_Resize(object sender, EventArgs e)
			{
			int h = Math.Max(pnlSignalAssignment.ClientSize.Height, 100);
			int w = pnlSignalAssignment.ClientSize.Width;

			trafficLightTreeView.Size = new System.Drawing.Size(w - 6, h - 3 - 23 - 6 - 3);
			freeNodeButton.Location = new System.Drawing.Point(w - 85 - 3, h - 26);
			}

		/// <summary>
		/// Function to download Image from website
		/// </summary>
		/// <param name="_URL">URL address to download image</param>
		/// <returns>Image</returns>
		public Image DownloadImage(string _URL)
			{
			Image _tmpImage = null;

			try
				{
				// Open a connection
				System.Net.HttpWebRequest _HttpWebRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(_URL);

				_HttpWebRequest.AllowWriteStreamBuffering = true;

				// You can also specify additional header values like the user agent or the referer: (Optional)
				_HttpWebRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1)";
				//_HttpWebRequest.Referer = "http://www.google.com/";

				// set timeout for 20 seconds (Optional)
				_HttpWebRequest.Timeout = 3000;

				// Request response:
				System.Net.WebResponse _WebResponse = _HttpWebRequest.GetResponse();

				// Open data stream:
				System.IO.Stream _WebStream = _WebResponse.GetResponseStream();

				// convert webstream to image
				_tmpImage = Image.FromStream(_WebStream);

				// Cleanup
				_WebResponse.Close();
				_WebResponse.Close();
				}
			catch (Exception _Exception)
				{
				// Error
				Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
				return null;
				}

			return _tmpImage;
			}

		private void btnSetWorldCoordinates_Click(object sender, EventArgs e)
			{
			string baseUrl = @"http://maps.google.com/maps/api/staticmap?zoom=19&size=635x628&sensor=false&maptype=satellite&center=";
			CultureInfo ci = new CultureInfo("en-US");
			int numx = 10;
			int numy = 10;
			satelliteImages = new Image[numx, numy];

			for (int i = 0; i < numx; ++i)
				{
				for (int j = 0; j < numy; ++j)
					{
					satelliteImages[i,j] = DownloadImage(baseUrl + (spinLatitude.Value - (0.001m * i)).ToString(ci) + "," + (spinLongitude.Value + (0.0017m * j)).ToString(ci));
					if (satelliteImages[i,j] != null)
						satelliteImages[i, j] = ResizeBitmap(new Bitmap(satelliteImages[i, j]), (int)(satelliteImages[i, j].Width * 1.739), (int)(satelliteImages[i, j].Height * 1.739));
					}
				}
// 				satelliteImages[0, 0] = DownloadImage(baseUrl + spinLatitude.Value.ToString(ci) + "," + spinLongitude.Value.ToString(ci));
// 			satelliteImages[1, 0] = DownloadImage(baseUrl + (spinLatitude.Value - 0.001m).ToString(ci) + "," + spinLongitude.Value.ToString(ci));
// 			satelliteImages[0, 1] = DownloadImage(baseUrl + spinLatitude.Value.ToString(ci) + "," + (spinLongitude.Value + 0.0017m).ToString(ci));
// 			satelliteImages[1, 1] = DownloadImage(baseUrl + (spinLatitude.Value - 0.001m).ToString(ci) + "," + (spinLongitude.Value + 0.0017m).ToString(ci));


			}

		private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
			{
			_dockingManager.SaveConfigToFile("GUILayout.xml");
			}

		private void pnlStatistics_Paint(object sender, PaintEventArgs e)
			{
			// gather NodeConnections for evaluating the statistics
			List<NodeConnection> connections = new List<NodeConnection>();
			if (selectedLineNodes.Count > 0)
				{
				foreach (LineNode ln in selectedLineNodes)
					{
					connections.AddRange(ln.nextConnections);
					}
				}
			else if (selectedNodeConnection != null)
				{
				connections.Add(selectedNodeConnection);
				}

			// check whether there's something to do
			if (connections.Count > 0)
				{
				int numBuckets = connections[0].statistics.Length;
				int maxInt = 1;
				double maxFloat = 1;

				// merge statistical data into one record
				NodeConnection.Statistics[] merged = new NodeConnection.Statistics[numBuckets];
				for (int i = 0; i < numBuckets; ++i)
					{
					foreach (NodeConnection nc in connections)
						{
						merged[i].numVehicles += nc.statistics[i].numVehicles;
						merged[i].numStoppedVehicles += nc.statistics[i].numStoppedVehicles;
						merged[i].sumOfVehicleVelocities += nc.statistics[i].sumOfVehicleVelocities;
						}
					maxInt = Math.Max(maxInt, merged[i].numVehicles);
					if (merged[i].numVehicles > 0)
						maxFloat = Math.Max(maxFloat, merged[i].sumOfVehicleVelocities / merged[i].numVehicles);
					}

				Pen blackPen = new Pen(Color.Black, 1.5f);
				Brush grayBrush = new SolidBrush(Color.LightGray);
				Brush redBrush = new SolidBrush(Color.Orange);

				// calculate data extent and derive transormation matrices
				maxFloat *= 1.2;
				maxInt *= 2;
				Matrix velocityMatrix = new Matrix((float)pnlStatistics.Width / numBuckets, 0, 0, (float)pnlStatistics.Height / (float)-maxFloat, 0, pnlStatistics.Height - 5);
				Matrix numVehicleMatrix = new Matrix((float)pnlStatistics.Width / numBuckets, 0, 0, (float)pnlStatistics.Height / -maxInt, 0, pnlStatistics.Height - 5);

				// render data
				for (int i = 1; i < numBuckets; ++i)
					{
					NodeConnection.Statistics s1 = merged[i];
					NodeConnection.Statistics s2 = merged[i-1];

					e.Graphics.SmoothingMode = SmoothingMode.HighSpeed;
					e.Graphics.Transform = numVehicleMatrix;
					e.Graphics.FillRectangle(grayBrush, i - 1, 0, 1, s1.numVehicles);
					e.Graphics.FillRectangle(redBrush, i - 1, 0, 1, s1.numStoppedVehicles);

					e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
					e.Graphics.Transform = velocityMatrix;
					float val1 = (s1.numVehicles == 0 ? 0 : (float)s1.sumOfVehicleVelocities / s1.numVehicles);
					float val2 = (s2.numVehicles == 0 ? 0 : (float)s2.sumOfVehicleVelocities / s2.numVehicles);

					e.Graphics.DrawLine(blackPen, i - 1, val1, i, val2);
					}
				}
			}

		private void DaGrid_Resize(object sender, EventArgs e)
			{
			UpdateDaGridClippingRect();
			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		private void cbRenderGrid_SizeChanged(object sender, EventArgs e)
			{
			spinGridSpacing.Location = new System.Drawing.Point(cbRenderGrid.Location.X + cbRenderGrid.Width + 5, spinGridSpacing.Location.Y);
			lblMeters.Location = new System.Drawing.Point(spinGridSpacing.Location.X + spinGridSpacing.Width + 5, lblMeters.Location.Y);
			}

		private void spinGridSpacing_ValueChanged(object sender, EventArgs e)
			{
			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		private void cbRenderGrid_CheckedChanged(object sender, EventArgs e)
			{
			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		private void cbStopSign_Click(object sender, EventArgs e)
			{
			foreach (LineNode ln in m_selectedLineNodes)
				{
				ln.stopSign = cbStopSign.Checked;
				}
			Invalidate(InvalidationLevel.ONLY_MAIN_CANVAS);
			}

		private void cbVehicleVelocityMapping_CheckedChanged(object sender, EventArgs e)
			{
			renderOptionsDaGrid.vehicleVelocityMapping = cbVehicleVelocityMapping.Checked;
			}

		private void btnReset_Click(object sender, EventArgs e)
			{
			killAllVehiclesButton_Click(this, new EventArgs());
			GlobalRandom.Instance.Reset((int)spinRandomSeed.Value);
			trafficVolumeSteuerung.ResetTrafficVolumes();
			GlobalTime.Instance.Reset();
			timelineSteuerung.AdvanceTo(0);
			}

		private void cmVelocityMapping_ColorMapChanged(object sender, CityTrafficSimulator.Tools.ColorMapControl.ColorMapChangedEventArgs e)
			{
			IVehicle._colormap = cmcVelocityMapping.colormap;
			NodeConnection._colormap = cmcVelocityMapping.colormap;
			}

		}
    }
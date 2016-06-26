
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

using CityTrafficSimulator.LoadingForm;
using CityTrafficSimulator.Timeline;
using CityTrafficSimulator.Tools;

namespace CityTrafficSimulator
	{
	/// <summary>
        /// Сохраняет / загружает текущее состояние nodeManagment и timelineManagment в/из XML-данные
	/// </summary>
	public static class XmlSaver
		{

		/// <summary>
            /// Сохраняте текущее состояние nodeManagment и timelineManagment в XML-данные filename
		/// </summary>
            /// <param name="filename">Имя файла сохраняемого файла</param>
            /// <param name="nodeSteuerung">NodeManagment</param>
            /// <param name="timelineSteuerung">TimelineManagment</param>
            /// <param name="trafficVolumeSteuerung">VerkehrManagment</param>
		/// <param name="ps">ProgramSettings</param>
		public static void SaveToFile(string filename, NodeManagment nodeSteuerung, TimelineManagment timelineSteuerung, Verkehr.TrafficControl trafficVolumeSteuerung, ProgramSettings ps)
			{
			try
				{
				// сначала задать имя для XMLWriter
				XmlWriterSettings xws = new XmlWriterSettings();
				xws.Indent = true;
				xws.NewLineHandling = NewLineHandling.Entitize;

				XmlWriter xw = XmlWriter.Create(filename, xws);

				// создать пустой XmlSerializerNamespaces Namespace
				XmlSerializerNamespaces xsn = new XmlSerializerNamespaces();
				xsn.Add("", "");

				xw.WriteStartElement("CityTrafficSimulator");

					// записать saveVersion
					xw.WriteStartAttribute("saveVersion");
					xw.WriteString("8");
					xw.WriteEndAttribute();

                    // сериализовать параметры программы
					XmlSerializer xsPS = new XmlSerializer(typeof(ProgramSettings));
					xsPS.Serialize(xw, ps, xsn);

					nodeSteuerung.SaveToFile(xw, xsn);
					timelineSteuerung.SaveToFile(xw, xsn);
					trafficVolumeSteuerung.SaveToFile(xw, xsn);

				xw.WriteEndElement();

				xw.Close();

				}
			catch (IOException e)
				{
				MessageBox.Show(e.Message);
				throw;
				}
			}


		/// <summary>
        /// Загрузка данных XML и попытаться их из сохраненного состояния, восстановить
		/// </summary>
        /// <param name="filename">Имя файла загружаемого файла</param>
        /// <param name="nodeSteuerung">NodeManagment должно быть вставлено в Layout</param>
        /// <param name="timelineSteuerung">TimelineManagment должно быть вставлено в LSA</param>
        /// <param name="trafficVolumeSteuerung">TrafficManagment загрузить в</param>
		public static ProgramSettings LoadFromFile(String filename, NodeManagment nodeSteuerung, TimelineManagment timelineSteuerung, Verkehr.TrafficControl trafficVolumeSteuerung)
			{
			LoadingForm.LoadingForm lf = new LoadingForm.LoadingForm();
			lf.Text = "Loading file '" + filename + "'...";
			lf.Show();

			lf.SetupUpperProgress("Loading Document...", 8);

			// Документ загружен
			XmlDocument xd = new XmlDocument();
			xd.Load(filename);

			// разобрать, сохранить версию файла
			int saveVersion = 0;
			XmlNode mainNode = xd.SelectSingleNode("//CityTrafficSimulator");
			XmlNode saveVersionNode = mainNode.Attributes.GetNamedItem("saveVersion");
			if (saveVersionNode != null)
				saveVersion = Int32.Parse(saveVersionNode.Value);
			else
				saveVersion = 0;

			ProgramSettings ps;
			if (saveVersion >= 8)
				{
				XmlNode xnlLineNode = xd.SelectSingleNode("//CityTrafficSimulator/ProgramSettings");
				TextReader tr = new StringReader(xnlLineNode.OuterXml);
				XmlSerializer xsPS = new XmlSerializer(typeof(ProgramSettings));
				ps = (ProgramSettings)xsPS.Deserialize(tr);
				}
			else
				{
                    // установить некоторые хорошие параметры по умолчанию
				ps = new ProgramSettings();

				ps._simSpeed = 1;
				ps._simSteps = 15;
				ps._simDuration = 300;
				ps._simRandomSeed = 42;

				ps._zoomLevel = 7;
				ps._renderQuality = 0;

				ps._renderStatistics = false;
				ps._renderVelocityMapping = false;
				ps._showFPS = false;

				ps._renderOptions = new NodeManagment.RenderOptions();
				ps._renderOptions.renderLineNodes = true;
				ps._renderOptions.renderNodeConnections = true;
				ps._renderOptions.renderVehicles = true;
				ps._renderOptions.performClipping = true;
				ps._renderOptions.clippingRect = new Rectangle(0, 0, 10000, 10000);
				ps._renderOptions.renderIntersections = false;
				ps._renderOptions.renderLineChangePoints = false;
				ps._renderOptions.renderLineNodeDebugData = false;
				ps._renderOptions.renderNodeConnectionDebugData = false;
				ps._renderOptions.renderVehicleDebugData = false;

				List<Color> tmp = new List<Color>();
				tmp.Add(Color.DarkRed);
				tmp.Add(Color.Yellow);
				tmp.Add(Color.DarkGreen);
				ps._velocityMappingColorMap = new Tools.ColorMap(tmp);
				}
			
			lf.StepUpperProgress("Parsing Network Layout...");
			List<ModelManager> toReturn = nodeSteuerung.LoadFromFile(xd, lf);

			lf.StepUpperProgress("Parsing Singnals...");
			timelineSteuerung.LoadFromFile(xd, nodeSteuerung.nodes, lf);

			lf.StepUpperProgress("Parsing TrafficControl Volume...");
			trafficVolumeSteuerung.LoadFromFile(xd, nodeSteuerung.nodes, lf);
			if (saveVersion < 5)
				{
				trafficVolumeSteuerung.ImportOldTrafficVolumeData(toReturn);
				}

			lf.StepUpperProgress("Done");
			lf.ShowLog();

			lf.Close();
			lf = null;

			return ps;
			}
		}
	}


using System;
using System.Collections.Generic;
using System.Text;

namespace CityTrafficSimulator
	{
	/// <summary>
	/// Singleton class for global simulation time
	/// </summary>
	public class GlobalTime
		{
		#region Singleton stuff

		/// <summary>
		/// Singleton instance
		/// </summary>
		private static readonly GlobalTime _instance = new GlobalTime();

		/// <summary>
		/// Singleton instance
		/// </summary>
		public static GlobalTime Instance
			{
			get { return _instance; }
			}

		/// <summary>
		/// Private Constructor - only to be used by singleton itsself.
		/// </summary>
		private GlobalTime()
			{
			currentTime = 0;
			cycleTime = 50;
			ticksPerSecond = 15;
			}

		#endregion

		#region Fields and Variables

		/// <summary>
		/// cycle time
		/// </summary>
		public double cycleTime { get; private set; }

		/// <summary>
		/// Number of ticks per second
		/// </summary>
		public double ticksPerSecond { get; private set; }

		/// <summary>
		/// current simulation time
		/// </summary>
		public double currentTime { get; private set; }
		
		/// <summary>
		/// current simulation time casted to float
		/// </summary>
		public float currentTimeAsFloat
			{
			get { return (float)currentTime; }
			}

		/// <summary>
		/// current tick number modulo cycle time
		/// </summary>
		public int currentCycleTick
			{
			get { return (int)((currentTime % cycleTime) * ticksPerSecond); }
			}


		#endregion

		#region Methods

		/// <summary>
		/// Advances current time by time.
		/// </summary>
		/// <param name="time">Time to add to current time</param>
		public void Advance(double time)
			{
			currentTime += time;
			}

		/// <summary>
		/// Resets current time to 0.
		/// </summary>
		public void Reset()
			{
			currentTime = 0;
			}

		/// <summary>
		/// Updates cylce time and ticks per second parameters.
		/// </summary>
		/// <param name="cycleTime">Cycle time</param>
		/// <param name="ticksPerSecond">Number of ticks/second</param>
		public void UpdateSimulationParameters(double cycleTime, double ticksPerSecond)
			{
			this.cycleTime = cycleTime;
			this.ticksPerSecond = ticksPerSecond;
			}

		#endregion

		}
	}

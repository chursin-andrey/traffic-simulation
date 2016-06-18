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
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CityTrafficSimulator.Vehicle
	{
	class Truck : IVehicle
		{
		public Truck(IVehicle.Physics p)
			{
			length = (GlobalRandom.Instance.Next(2) == 0) ? 100 : 165;

			_physics = p;
			color = Color.FromArgb(GlobalRandom.Instance.Next(256), GlobalRandom.Instance.Next(256), GlobalRandom.Instance.Next(256));

			// maximale Beschleunigung
			a = 0.6;

			// komfortable Bremsverzögerung
			b = 0.7;


			// etwas Zufall:
			a *= (GlobalRandom.Instance.NextDouble() + 0.5);
			b *= (GlobalRandom.Instance.NextDouble() + 0.5);
			s0 *= (GlobalRandom.Instance.NextDouble() + 0.5);
			T *= (GlobalRandom.Instance.NextDouble() + 0.5);

			_physics.targetVelocity += ((GlobalRandom.Instance.NextDouble() - 0.5) * 4);
			_vehicleType = VehicleTypes.CAR;
			}
	
		/// <summary>
		/// Berechnet die Absolute Position zur Bogenlängenposition pos.
		/// Kann auch mit negativen Positionsangaben umgehen
		/// </summary>
		/// <param name="pos">Bogenlängenposition</param>
		/// <returns>Weltkoordinaten zu Bogenlängenposition pos</returns>
		private Vector2 GetPositionAbsAtArcPos(double pos)
			{
			if (pos >= 0)
				return currentNodeConnection.lineSegment.AtPosition(pos);
			else
				{
				double remainingDistance = -pos;
				foreach (NodeConnection nc in visitedNodeConnections)
					{
					if (nc.lineSegment.length < remainingDistance)
						{
						remainingDistance -= nc.lineSegment.length;
						continue;
						}
					else
						{
						return nc.lineSegment.AtPosition(nc.lineSegment.length - remainingDistance);
						}
					}
				}
			return state.positionAbs;
			}

		/// <summary>
		/// Generates the GraphicsPath for rendering the vehicle. Should be overloaded by subclasses with distinct rendering.
		/// </summary>
		/// <returns>A GraphicsPath in world coordinates for rendering the vehicle at the current position.</returns>
		protected override GraphicsPath BuildGraphicsPath()
			{
			GraphicsPath toReturn = new GraphicsPath();

			// LKW
			if (_length == 100)
				{
				Vector2[] positions = {
					state.positionAbs,
					GetPositionAbsAtArcPos(currentPosition - 100),
									  };
				if ((positions[0] - positions[1]).IsNotZeroVector())
					{
					Vector2[] normals = {
					(positions[0] - positions[1]).RotatedClockwise.Normalized,
								  };

					PointF[] frontPoints = { 
						positions[0]  -  10*normals[0],
						positions[0]  +  10*normals[0],
						positions[1]  +  10*normals[0],
						positions[1]  -  10*normals[0],
					};

					toReturn.AddPolygon(frontPoints);
					}
				}
			// Sattelzug
			else
				{
				Vector2[] positions = {
					state.positionAbs,
					GetPositionAbsAtArcPos(currentPosition - 40),
					GetPositionAbsAtArcPos(currentPosition - 45),
					GetPositionAbsAtArcPos(currentPosition - 165)
									  };
				if ((positions[0] - positions[1]).IsNotZeroVector() && (positions[2] - positions[3]).IsNotZeroVector())
					{
					Vector2[] normals = {
					(positions[0] - positions[1]).RotatedClockwise.Normalized,
					(positions[2] - positions[3]).RotatedClockwise.Normalized
								  };

					PointF[] frontPoints = { 
						positions[0]  -  10*normals[0],
						positions[0]  +  10*normals[0],
						positions[1]  +  10*normals[0],
						positions[1]  -  10*normals[0],
					};
					PointF[] backPoints = { 
						positions[2]  -  10*normals[1],
						positions[2]  +  10*normals[1],
						positions[3]  +  10*normals[1],
						positions[3]  -  10*normals[1],
	                };

					toReturn.AddPolygon(frontPoints);
					toReturn.AddPolygon(backPoints);
					}
				}

			return toReturn;
			}

		/// <summary>
		/// Prüfe, ob ich auf der NodeConnection nc fahren darf
		/// </summary>
		/// <param name="nc">zu prüfende NodeConnection</param>
		/// <returns>nc.tramAllowed</returns>
		public override bool CheckNodeConnectionForSuitability(NodeConnection nc)
			{
			return nc.carsAllowed;
			}
		}
	}

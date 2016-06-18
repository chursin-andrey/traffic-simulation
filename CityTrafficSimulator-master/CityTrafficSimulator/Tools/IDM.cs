using System;
using System.Collections.Generic;
using System.Text;

namespace CityTrafficSimulator
    {
	/// <summary>
	/// абстрактный класс для Intelligent Driver Model
	/// </summary>
    public abstract class IDM
        {
        #region Konstanten
        /*
         * Berechnungen nach dem IDM Schema der TU Dresden
         * http://www.cs.wm.edu/~coppit/csci435-spring2005/project/MOBIL.pdf
         */
        /*double T = 10; // Zeitlicher Sicherheitsabstand
        double a = 2; // Maxmale Beschleunigung°
        double b = 3; // komfortable Bremsverzögerung
        double s0 = 20; // Mindestabstand im Stau
        */

		/*
		 * Konstanten in s, m/s und m/s²
		 *
        protected double T = 2; // Zeitlicher Sicherheitsabstand
		protected double a = 1.2; // Maxmale Beschleunigung
		protected double b = 1.5; // komfortable Bremsverzögerung
		protected double s0 = 10; // Mindestabstand im Stau
		 //*/
		
		/*
		 * Konstanten in s/20, Pixel/20s^2
		 * 1 Pixel = 1 dm	
		 */
		/// <summary>
		/// Zeitlicher Sicherheitsabstand
		/// </summary>
		protected double T = 1.4;
		/// <summary>
		/// Zeitlicher Sicherheitsabstand
		/// </summary>
		public double SafetyTime { get { return T; } }

		/// <summary>
		/// maximale Beschleunigung
		/// </summary>
		protected double a = 1.2;

		/// <summary>
		/// komfortable Bremsverzögerung
		/// </summary>
		protected double b = 1.5;

		/// <summary>
		/// Mindestabstand im Stau
		/// </summary>
		public double s0 = 20;


		/* MOBIL PARAMETER */


		/// <summary>
		/// Politeness-Faktor von MOBIL
		/// </summary>
		protected double p = 0.2;

		/// <summary>
		/// Mindest-Vorteilswert für Spurwechsel
		/// </summary>
		protected double lineChangeThreshold = 0.75;

		/// <summary>
		/// maximale sichere Bremsverzögerung
		/// </summary>
		protected double bSave = -3;
		//*/

		#endregion

		/// <summary>
        /// Рассчитывает желаемое расстояние до IDM
		/// </summary>
		/// <param name="velocity">собственная скорость</param>
        /// <param name="vDiff">Дифференциальная скорость до впереди идущего автомобиля автомобиля</param>
		/// <returns></returns>
		public double CalculateWantedDistance(double velocity, double vDiff)
			{
                // вычислить s * = желаемое состояние
			double ss = s0 + T * velocity + (velocity * vDiff) / (2 * Math.Sqrt(a * b));

			if (ss < s0)
				{
				ss = s0;
				}

			return ss;
			}

		/// <summary>
        /// Вычислить свободное ускорение после IDM 
		/// http://www.cs.wm.edu/~coppit/csci435-spring2005/project/MOBIL.pdf
		/// </summary>
        /// <param name="velocity">текущая скорость</param>
        /// <param name="desiredVelocity">желаемая скорость</param>
        /// <param name="distance">Расстояние до впереди едущего автомобиля</param>
        /// <param name="vDiff">Разница в скорости впереди едущего автомобиляg</param>
		/// <returns></returns>
        public double CalculateAcceleration(double velocity, double desiredVelocity, double distance, double vDiff)
            {
                // вычислить s * = желаемое состояние
			double ss = CalculateWantedDistance(velocity, vDiff);

            // Вычислить новую скорость
            double vNeu = a * (1 - Math.Pow((velocity / desiredVelocity), 2) - Math2.Square(ss / distance));

            return vNeu;
            }

		/// <summary>
        /// Вычислить ускорение свободно после IDM, когда ни одно транспортное средство не выезжает вперед
		/// </summary>
		/// <param name="velocity">собственная скорость</param>
		/// <param name="desiredVelocity">желаемая скорость</param>
		/// <returns></returns>
		public double CalculateAcceleration(double velocity, double desiredVelocity)
			{
                // Вычислить новую скорость
			double vNeu = a * (1 - Math.Pow((velocity / desiredVelocity), 2) );

			return vNeu;
			}

		/// <summary>
        /// Вычислить свободное ускорение  после IDM методом Гойна (консистенция порядка 2!)
		/// http://www.cs.wm.edu/~coppit/csci435-spring2005/project/MOBIL.pdf
		/// </summary>
		/// <param name="velocity">текущая скорость</param>
		/// <param name="desiredVelocity">желаемая скорость</param>
		/// <param name="distance">Расстояние до впереди едущего автомобиля</param>
        /// <param name="vDiff">Разница в скорости впереди едущего автомобиля</param>
		/// <returns></returns>
		public double CalculateAccelerationHeun(double velocity, double desiredVelocity, double distance, double vDiff)
			{
                // Первое приближение:
                // вычислить s * = желаемое состояние
				double ss1 = CalculateWantedDistance(velocity, vDiff);
                // Вычислить новую скорость
				double vNeu = a * (1 - Math.Pow((velocity / desiredVelocity), 2) - Math2.Square(ss1 / distance));

                // Второе приближение:
                // вычислить s * = желаемое состояние
				double ss2 = CalculateWantedDistance(velocity + vNeu, vDiff + vNeu);
                // Вычислить новую скорость
				vNeu += a * (1 - Math.Pow(((velocity + vNeu) / desiredVelocity), 2) - Math2.Square(ss2 / distance));

			return vNeu/2;
			}



		/// <summary>
        /// Вычислить ускорение, если желаемое расстояние уже известно в IDM
		/// </summary>
		/// <param name="velocity">текущая скорость</param>
		/// <param name="desiredVelocity">желаемая скорость</param>
        /// <param name="distance">текущее расстояние</param>
		/// <param name="wantedDistance">желаемое расстояние</param>
        /// <param name="vDiff">разница в скорости</param>
		/// <returns></returns>
		public double CalculateAcceleration(double velocity, double desiredVelocity, double distance, double wantedDistance, double vDiff)
			{
                // Вычислить новую скорость
			double vNeu = a * (1 - Math.Pow((velocity / desiredVelocity), 2) - Math2.Square(wantedDistance / distance));

			return vNeu;
			}


        }
    }

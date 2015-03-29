﻿using UnityEngine;
using UnityFreeFlight;


namespace UnityFreeFlight {

	public class FlightPhysics {

		//Wing Properties
		[Header ("Wing Properties")]
		public float wingChord = .7f; //in meters
		public float wingSpan = 1.715f;  //in meters
		public float wingArea { get { return wingChord * wingSpan; } }
		public float aspectRatio { get { return wingSpan / wingChord; } }
		public float weight = 1f;	// in kilograms
		public float baseDrag = 1f;
		public float wingEfficiency = 0.9f; 

		[HideInInspector]
		public float wingExposureArea;
		public float leftWingExposure;
		public float rightWingExposure;

		private float _angleOfAttack;
		public float angleOfAttack { get { return _angleOfAttack; } }
		private float _airspeed;
		public float airspeed { get { return _airspeed; } }

		//Lift variables -- Forces are in Newtons/Second
		private float _liftCoefficient;
		public float liftCoefficient { get { return _liftCoefficient; } }
		private float _liftForce;
		public float liftForce { get { return _liftForce; } }
		private Vector3 _liftForceVector;
		public Vector3 liftForceVector { get { return _liftForceVector; } }

		//Drag Variables -- Forces are in Newtons/Second
		private float _dragCoefficient;
		public float dragCoefficient { get { return _dragCoefficient; } }
		private float _liftInducedDragForce;
		public float liftInducedDragForce { get { return _liftInducedDragForce; } }
		private float _formDragForce;
		public float formDragForce { get { return _formDragForce; } }
		private Vector3 _dragForceVector;
		public Vector3 dragForceVector { get { return _dragForceVector; } }

		public FlightPhysics () {
			//open the wings
			setWingExposure (1f, 1f);
		}

		public void setWingExposure(float cleftWingExposure, float crightWingExposure) {
			//Make sure area is never actually zero, as this is technically impossible and 
			//causes physics to fail.
			leftWingExposure = (cleftWingExposure == 0.0f) ? 0.01f : cleftWingExposure;
			rightWingExposure = (crightWingExposure == 0.0f) ? 0.01f : crightWingExposure;
			wingExposureArea = wingSpan * wingChord * (leftWingExposure + rightWingExposure) / 2;
		}

		public bool wingsOpen() {
			if (wingExposureArea == wingArea)
				return true;
			return false;
		}

		public void applyPhysics(Rigidbody rigidbody) {

			//TODO: swap rigidbody.velocity for the relative airspeed. Current calculation does not take into
			//account wind or other forces.
			physicsTick (rigidbody.velocity, rigidbody.rotation);

			if (rigidbody.isKinematic)
				return;
			
			rigidbody.rotation = getBankedTurnRotation(rigidbody.rotation);
			rigidbody.velocity = Vector3.Lerp (rigidbody.velocity, 
			                                   getDirectionalVelocity(rigidbody.rotation, rigidbody.velocity), 
			                                   Time.deltaTime);	

			rigidbody.AddForce (_liftForceVector * Time.deltaTime);
			rigidbody.AddForce (_dragForceVector * Time.deltaTime);
		}

		/// <summary>
		/// Calculate lift, drag, and angle of attack for this instant.
		/// </summary>
		public void physicsTick(Vector3 relativeAirVelocity, Quaternion rotation) {
			if (relativeAirVelocity == Vector3.zero)
				return;

			_airspeed = relativeAirVelocity.magnitude;
			
			//These are required for computing lift and drag	
			_angleOfAttack = getAngleOfAttack(rotation, relativeAirVelocity);	
					
			//Calculate lift 
			_liftCoefficient = getLiftCoefficient(_angleOfAttack);
			_liftForce = getLift(_airspeed, wingExposureArea, _liftCoefficient);
			_liftForceVector = getLiftForceVector(relativeAirVelocity, _liftForce);

			//Calculate drag
			_dragCoefficient = getDragCoefficient (_angleOfAttack);
			_liftInducedDragForce = getLiftInducedDrag (_liftForce, _airspeed, wingExposureArea, wingEfficiency, aspectRatio);
			_formDragForce = getFormDrag (_airspeed, wingExposureArea, _dragCoefficient);
			_dragForceVector = getDragForceVector (relativeAirVelocity, _liftInducedDragForce + _formDragForce);

		}

		/// <summary>
		/// Get a Vector 3 meausring the direction of the 
		/// lift and the magnitude of the force.
		/// </summary>
		/// <returns>The lift 3d directional force vector.</returns>
		/// <param name="relativeAirVelocity">Relative air velocity.</param>
		/// <param name="wingArea">Total wing area capable of generating lift</param>
		/// <param name="liftCoeff">The wings lift coefficient</param>
		public Vector3 getLiftForceVector(Vector3 relativeAirVelocity, float lift) {
			if (relativeAirVelocity != Vector3.zero || lift == 0) {
				return Quaternion.LookRotation(relativeAirVelocity) * Vector3.up * liftForce;
			}
			return Vector3.zero;
		}
		
		
		/// <summary>
		/// 	Get the current lift force on the object based on the paramaters. 
		/// 
		/// 	All dimensions must be in metric.
		/// </summary>
		/// <returns>The lift in Newtons</returns>
		/// <param name="velocity">Velocity Meters/Second</param>
		/// <param name="pressure">Pressure (something)</param>
		/// <param name="area">Area Meters^2</param>
		/// <param name="liftCoff">Lift coff. (dimensionless)</param>
		public float getLift(float velocity, float area, float liftCoff) {
			return velocity * velocity * WorldPhysics.pressure * area * liftCoff;
		}

		/// <summary>
		/// Get the lift coefficient at the specified angle of attack.
		/// </summary>
		/// <returns>The lift coefficient.</returns>
		/// <param name="angleDegrees">Angle degrees.</param>
		public float getLiftCoefficient(float angleDegrees) {
			float cof;
			//			if(angleDegrees > 40.0f)
			//				cof = 0.0f;
			//			if(angleDegrees < 0.0f)
			//				cof = angleDegrees/90.0f + 1.0f;
			//			else
			//				cof = -0.0024f * angleDegrees * angleDegrees + angleDegrees * 0.0816f + 1.0064f;
			//Formula based on theoretical thin airfoil theory. We get a very rough estimate here,
			//and this does not take into account wing aspect ratio
			cof = 2 * Mathf.PI * angleDegrees * Mathf.Deg2Rad;
			return cof;	
		}

		/// <summary>
		/// Get 3D vector of the drag force
		/// </summary>
		/// <returns>The drag force vector.</returns>
		/// <param name="relativeAirVelocity">Relative air velocity.</param>
		/// <param name="drag">Drag.</param>
		public Vector3 getDragForceVector(Vector3 relativeAirVelocity, float drag) {
			if (relativeAirVelocity != Vector3.zero)
				return Quaternion.LookRotation(relativeAirVelocity) * Vector3.back * drag;
			return Vector3.zero;
		}

		/// <summary>
		/// Get the drag caused by the shape and size of the airfoil. 
		/// </summary>
		/// <returns>The form drag.</returns>
		/// <param name="velocity">airspeed</param>
		/// <param name="area">current wing exposure</param>
		/// <param name="dragCoff">Drag coff.</param>
		public float getFormDrag(float velocity, float area, float dragCoff) {
			return .5f * WorldPhysics.pressure * velocity * velocity * area * dragCoff;
		}

		/// <summary>
		/// Get drag created by generated lift
		/// </summary>
		/// <returns>The lift induced drag.</returns>
		/// <param name="lift">Lift in newtons per second</param>
		/// <param name="velocity">Velocity in meters/second.</param>
		/// <param name="area">current wing area exposure</param>
		/// <param name="wingefficiency">Efficiency of wing as an airfoil</param>
		/// <param name="aspectR">Aspect ratio of wing.</param>
		public float getLiftInducedDrag(float lift, float velocity, float area, float wingefficiency, float aspectR) {
			return (lift*lift) / (.5f * WorldPhysics.pressure * velocity * velocity * area * Mathf.PI * wingefficiency * aspectR);
		}

		public float getDragCoefficient(float angleDegrees) {
			float cof;
			//if(angleDegrees < -20.0f)
			//	cof = 0.0f;
			//else
			cof = .0039f * angleDegrees * angleDegrees + .025f;
			return cof;
		}
		
		//Rotates the object down when velocity gets low enough to simulate "stalling"
		public Quaternion getStallRotation (Quaternion curRot, float velocity) {
			//This equation isn't based on any real-world physics. But it seems to work pretty well as is.
			float pitchRotationSpeed = 10.0f / (velocity * velocity);
			Quaternion pitchrot = Quaternion.LookRotation (Vector3.down);
			Quaternion newRot = Quaternion.Lerp (curRot, pitchrot, Mathf.Abs (pitchRotationSpeed) * Time.deltaTime);
			return newRot;
		}
		
		//When we do a turn, we don't just want to rotate our character. We want their
		//velocity to match the direction they are facing. 
		public Vector3 getDirectionalVelocity(Quaternion theCurrentRotation, Vector3 theCurrentVelocity) {
			Vector3 vel = theCurrentVelocity;
			
			vel = (theCurrentRotation * Vector3.forward).normalized * theCurrentVelocity.magnitude;	
			//Debug.Log (string.Format ("velocity: {0}, New Velocity {1} mag1: {2}, mag2 {3}", theCurrentVelocity, vel, theCurrentVelocity.magnitude, vel.magnitude));
			return vel;
		}
		
		//Get new yaw and roll, store the value in newRotation
		public Quaternion getBankedTurnRotation(Quaternion theCurrentRotation) {
			//Quaternion getBankedTurnRotation(float curZRot, float curLift, float curVel, float mass) {
			// The physics of a banked turn is as follows
			//  L * Sin(0) = M * V^2 / r
			//	L is the lift acting on the aircraft
			//	θ0 is the angle of bank of the aircraft
			//	m is the mass of the aircraft
			//	v is the true airspeed of the aircraft
			//	r is the radius of the turn	
			//
			// Currently, we'll keep turn rotation simple. The following is not based on the above, but it provides
			// A pretty snappy mechanism for getting the job done.
			//Apply Yaw rotations. Yaw rotation is only applied if we have angular roll. (roll is applied directly by the 
			//player)
			Quaternion angVel = Quaternion.identity;
			//Get the current amount of Roll, it will determine how much yaw we apply.
			float zRot = Mathf.Sin (theCurrentRotation.eulerAngles.z * Mathf.Deg2Rad) * Mathf.Rad2Deg;
			//We don't want to change the pitch in turns, so we'll preserve this value.
			float prevX = theCurrentRotation.eulerAngles.x;
			//Calculate the new rotation. The constants determine how fast we will turn.
			Vector3 rot = new Vector3(0, -zRot * 0.8f, -zRot * 0.5f) * Time.deltaTime;
			
			//Apply the new rotation 
			angVel.eulerAngles = rot;
			angVel *= theCurrentRotation;	
			angVel.eulerAngles = new Vector3(prevX, angVel.eulerAngles.y, angVel.eulerAngles.z);
			
			//Done!
			return angVel;	
		}
		
		
		//Return angle of attack based on objects current directional Velocity and rotation
		public float getAngleOfAttack(Quaternion theCurrentRotation, Vector3 theCurrentVelocity) {
			//Angle of attack is basically the angle air strikes a wing. Imagine a plane flying 
			//at exact level altitude into a stable air mass. The air passes over the wing very
			//efficiently, so we have an AOA of zero. When the plane pitches back, air starts to
			//strike the bottom of the wing, creating more drag and lift. The angle of pitch 
			//relative to the airmass is called angle of attack. 
			float theAngleOfAttack;
			//The direction we are going
			Vector3 dirVel;
			
			//We need speed in order to get directional velocity.
			if (theCurrentVelocity != Vector3.zero) {
				//Find the direction we are going
				dirVel = Quaternion.LookRotation(theCurrentVelocity) * Vector3.up;
			} else {
				//This has the effect of 'imagining' the craft is on a level flight
				//moving forward. Since angle of attack means nothing at zero speed,
				//this is simply a way to visualize it when we are dead stopped.
				dirVel = Vector3.up;
			}
			
			//		Debug.Log(string.Format ("Directional Velocity : {0}", dirVel));
			
			//Find the rotation directly in front of us
			Vector3 forward = theCurrentRotation * Vector3.forward;
			//The dot product returns a positive or negative float if we are 'pitched up' towards
			//our air mass, or 'pitched down into' our airmass. Remember that our airmass also has
			//a velocity coming towards us, which is somewhere between coming directly at us in
			//level flight, or if we are falling directly towards the ground, it is coming directly
			//below us. 
			
			//The dot product always returns between -1 to 1, so taking the ArcSin will give us
			//a reasonable angle of attack. Remember to convert to degrees from Radians. 
			theAngleOfAttack = Mathf.Asin(Vector3.Dot(forward, dirVel)) * Mathf.Rad2Deg;
			//HACK: I'm not sure why, but sometimes this returns NAN. The reason should be found,
			//and fixed. For now, well just check if it's crazy and return sane things instead.
			return (float.IsNaN (theAngleOfAttack)) ? 0f : theAngleOfAttack;
		}
	}
}
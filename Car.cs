using UnityEngine;
using System.Collections.Generic;

public class Car : MonoBehaviour {

	[SerializeField]
	bool IsPlayerControlled = false;

	[SerializeField]
	[Range(0f, 1f)]
	float CGHeight = 0.55f;

	[SerializeField]
	[Range(0f, 2f)]
	float InertiaScale = 1f;

	[SerializeField]
	float BrakePower = 12000;

	[SerializeField]
	float EBrakePower = 5000;

	[SerializeField]
	[Range(0f, 1f)]
	float WeightTransfer = 0.35f;

	[SerializeField]
	[Range(0f, 1f)]
	float MaxSteerAngle = 0.75f;

	[SerializeField]
	[Range(0f, 20f)]
	float CornerStiffnessFront = 5.0f;

	[SerializeField]
	[Range(0f, 20f)]
	float CornerStiffnessRear = 5.2f;

	[SerializeField]
	[Range(0f, 20f)]
	float AirResistance = 2.5f;

	[SerializeField]
	[Range(0f, 20f)]
	float RollingResistance = 8.0f;

	[SerializeField]
	[Range(0f, 1f)]
	float EBrakeGripRatioFront = 0.9f;

	[SerializeField]
	[Range(0f, 5f)]
	float TotalTireGripFront = 2.5f;

	[SerializeField]
	[Range(0f, 1f)]
	float EBrakeGripRatioRear = 0.4f;

	[SerializeField]
	[Range(0f, 5f)]
	float TotalTireGripRear = 2.5f;

	[SerializeField]
	[Range(0f, 5f)]
	float SteerSpeed = 2.5f;

	[SerializeField]
	[Range(0f, 5f)]
	float SteerAdjustSpeed = 1f;

	[SerializeField]
	[Range(0f, 1000f)]
	float SpeedSteerCorrection = 300f;

	[SerializeField]
	[Range(0f, 20f)]
	float SpeedTurningStability = 10f;

	[SerializeField]
	[Range(0f, 10f)]
	float AxleDistanceCorrection = 2f;

	// --- New params for kinematic/dynamic blending
	[SerializeField]
	[Tooltip("Ниже этой скорости — преимущественно кinematic bicycle моделирование")]
	float KinematicModeSpeed = 0.5f; // m/s

	[SerializeField]
	[Tooltip("Ширина линейного бленда в м/с")]
	float KinematicBlendRange = 2.0f; // m/s

	public float SpeedKilometersPerHour {
		get {
			return Rigidbody2D.velocity.magnitude * 18f / 5f;
		}
	}

	// Variables that get initialized via code
	float Inertia = 1;
	float WheelBase = 1;
	float TrackWidth = 1;

	// Private vars
	float HeadingAngle;
	float AbsoluteVelocity;
	float AngularVelocity;
	float SteerDirection;
	float SteerAngle;

	Vector2 Velocity;
	Vector2 Acceleration;
	Vector2 LocalVelocity;
	Vector2 LocalAcceleration;

	float Throttle;
	float Brake;
	float EBrake;

	Rigidbody2D Rigidbody2D;

	Axle AxleFront;
	Axle AxleRear;
	Engine Engine;

	GameObject CenterOfGravity;
	GameObject CameraView;

	// Debug values updated in FixedUpdate for OnDrawGizmos
	float debugDynamicBlend = 0f;
	float debugYawRateKinematic = 0f;

	void Awake() {

		CameraView = GameObject.Find ("CameraView").gameObject;

		Rigidbody2D = GetComponent<Rigidbody2D> ();
		CenterOfGravity = transform.Find ("CenterOfGravity").gameObject;

		AxleFront = transform.Find ("AxleFront").GetComponent<Axle>();
		AxleRear = transform.Find ("AxleRear").GetComponent<Axle>();

		Engine = transform.Find ("Engine").GetComponent<Engine>();

		Init ();
	}

	void Init() {

		Velocity = Vector2.zero;
		AbsoluteVelocity = 0;

		// Dimensions
		AxleFront.DistanceToCG = Vector2.Distance(CenterOfGravity.transform.position, AxleFront.transform.Find("Axle").transform.position);
		AxleRear.DistanceToCG = Vector2.Distance(CenterOfGravity.transform.position, AxleRear.transform.Find("Axle").transform.position);
		// Extend the calculations past actual car dimensions for better simulation
		AxleFront.DistanceToCG *= AxleDistanceCorrection;
		AxleRear.DistanceToCG *= AxleDistanceCorrection;
			
		WheelBase = AxleFront.DistanceToCG + AxleRear.DistanceToCG;
		Inertia = Rigidbody2D.mass * InertiaScale;

		// Set starting angle of car
		Rigidbody2D.rotation = transform.rotation.eulerAngles.z;
		HeadingAngle = (Rigidbody2D.rotation + 90) * Mathf.Deg2Rad;
	}

	void Start() {
		
		AxleFront.Init (Rigidbody2D, WheelBase);
		AxleRear.Init (Rigidbody2D, WheelBase);

		TrackWidth = Vector2.Distance(AxleRear.TireLeft.transform.position, AxleRear.TireRight.transform.position);
	}

	void Update() {

		if (IsPlayerControlled) {

			// Handle Input
			Throttle = 0;
			Brake = 0;
			EBrake = 0;

			if (Input.GetKey (KeyCode.UpArrow)) {
				Throttle = 1;
			} else if (Input.GetKey (KeyCode.DownArrow)) { 
				//Brake = 1;
				Throttle = -1;
			} 
			if(Input.GetKey(KeyCode.Space))	{
				EBrake = 1;
			}

			float steerInput = 0;
			if(Input.GetKey(KeyCode.LeftArrow))	{
				steerInput = 1;
			}
			else if(Input.GetKey(KeyCode.RightArrow)) {
				steerInput = -1;
			}

			if (Input.GetKeyDown (KeyCode.A)) {
				Engine.ShiftUp();
			} else if (Input.GetKeyDown (KeyCode.Z)) {
				Engine.ShiftDown();
			}

			// Apply filters to our steer direction
			SteerDirection = SmoothSteering (steerInput);
			SteerDirection = SpeedAdjustedSteering (SteerDirection);

			// Calculate the current angle the tires are pointing
			SteerAngle = SteerDirection * MaxSteerAngle;

			// Set front axle tires rotation
			AxleFront.TireRight.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * SteerAngle);
			AxleFront.TireLeft.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * SteerAngle);
		}			

		// Calculate weight center of four tires
		// This is just to draw that red dot over the car to indicate what tires have the most weight
		Vector2 pos = Vector2.zero;
		if (LocalAcceleration.magnitude > 1f) {

			float wfl = Mathf.Max (0, (AxleFront.TireLeft.ActiveWeight - AxleFront.TireLeft.RestingWeight));
			float wfr = Mathf.Max (0, (AxleFront.TireRight.ActiveWeight - AxleFront.TireRight.RestingWeight));
			float wrl = Mathf.Max (0, (AxleRear.TireLeft.ActiveWeight - AxleRear.TireLeft.RestingWeight));
			float wrr = Mathf.Max (0, (AxleRear.TireRight.ActiveWeight - AxleRear.TireRight.RestingWeight));

			pos = (AxleFront.TireLeft.transform.localPosition) * wfl +
				(AxleFront.TireRight.transform.localPosition) * wfr +
			    (AxleRear.TireLeft.transform.localPosition) * wrl +
				(AxleRear.TireRight.transform.localPosition) * wrr;
		
			float weightTotal = wfl + wfr + wrl + wrr;

			if (weightTotal > 0) {
				pos /= weightTotal;
				pos.Normalize ();
				pos.x = Mathf.Clamp (pos.x, -0.6f, 0.6f);
			} else {
				pos = Vector2.zero;
			}
		}

		// Update the "Center Of Gravity" dot to indicate the weight shift
		CenterOfGravity.transform.localPosition = Vector2.Lerp (CenterOfGravity.transform.localPosition, pos, 0.1f);

		// Skidmarks
		if (Mathf.Abs (LocalAcceleration.y) > 18 || EBrake == 1) {
			AxleRear.TireRight.SetTrailActive (true);
			AxleRear.TireLeft.SetTrailActive (true);
		} else {
			AxleRear.TireRight.SetTrailActive (false);
			AxleRear.TireLeft.SetTrailActive (false);
		}

		// Automatic transmission
		Engine.UpdateAutomaticTransmission (Rigidbody2D);

        // Update camera
        if (IsPlayerControlled)
        {
            CameraView.transform.position = this.transform.position;
        }
	}
			
	void FixedUpdate() {

		// Update from rigidbody to retain collision responses
		Velocity = Rigidbody2D.velocity;
		HeadingAngle = (Rigidbody2D.rotation + 90) * Mathf.Deg2Rad;

		float sin = Mathf.Sin(HeadingAngle);
		float cos = Mathf.Cos(HeadingAngle);

		// Get local velocity
		LocalVelocity.x = cos * Velocity.x + sin * Velocity.y;
		LocalVelocity.y = cos * Velocity.y - sin * Velocity.x;

		// Weight transfer
		float transferX = WeightTransfer * LocalAcceleration.x * CGHeight / WheelBase;
		float transferY = WeightTransfer * LocalAcceleration.y * CGHeight / TrackWidth * 20;		//exagerate the weight transfer on the y-axis

		// Weight on each axle
		float weightFront = Rigidbody2D.mass * (AxleFront.WeightRatio * -Physics2D.gravity.y - transferX);
		float weightRear = Rigidbody2D.mass * (AxleRear.WeightRatio * -Physics2D.gravity.y + transferX);

		// Weight on each tire
		AxleFront.TireLeft.ActiveWeight = weightFront - transferY;
		AxleFront.TireRight.ActiveWeight = weightFront + transferY;
		AxleRear.TireLeft.ActiveWeight = weightRear - transferY;
		AxleRear.TireRight.ActiveWeight = weightRear + transferY;
			
		// Velocity of each tire (these are approximations for rotational velocity contribution)
		AxleFront.TireLeft.AngularVelocity = AxleFront.DistanceToCG * AngularVelocity;
		AxleFront.TireRight.AngularVelocity = AxleFront.DistanceToCG * AngularVelocity;
		AxleRear.TireLeft.AngularVelocity = -AxleRear.DistanceToCG * AngularVelocity;
		AxleRear.TireRight.AngularVelocity = -AxleRear.DistanceToCG *  AngularVelocity;

		// Slip angle
		// Protect against division by very small forward velocity: use min forward speed or rely on kinematic/dynamic blend
		float minLongVel = 0.5f;
		float denom = Mathf.Max(Mathf.Abs(LocalVelocity.x), minLongVel);

		AxleFront.SlipAngle = Mathf.Atan2(LocalVelocity.y + AxleFront.AngularVelocity, denom) - Mathf.Sign(LocalVelocity.x) * SteerAngle;
		AxleRear.SlipAngle = Mathf.Atan2(LocalVelocity.y + AxleRear.AngularVelocity, denom);

		// Brake and Throttle power
		float activeBrake = Mathf.Min(Brake * BrakePower + EBrake * EBrakePower, BrakePower);
		float activeThrottle = (Throttle * Engine.GetTorque (Rigidbody2D)) * (Engine.GearRatio * Engine.EffectiveGearRatio);

		// Torque of each tire (rear wheel drive assumed)
		AxleRear.TireLeft.Torque = activeThrottle / AxleRear.TireLeft.Radius;
		AxleRear.TireRight.Torque = activeThrottle / AxleRear.TireRight.Radius;

		// Grip and Friction of each tire
		AxleFront.TireLeft.Grip = TotalTireGripFront * (1.0f - EBrake * (1.0f - EBrakeGripRatioFront));
		AxleFront.TireRight.Grip = TotalTireGripFront * (1.0f - EBrake * (1.0f - EBrakeGripRatioFront));
		AxleRear.TireLeft.Grip = TotalTireGripRear * (1.0f - EBrake * (1.0f - EBrakeGripRatioRear));
		AxleRear.TireRight.Grip = TotalTireGripRear * (1.0f - EBrake * (1.0f - EBrakeGripRatioRear));

		AxleFront.TireLeft.FrictionForce = Mathf.Clamp(-CornerStiffnessFront * AxleFront.SlipAngle, -AxleFront.TireLeft.Grip, AxleFront.TireLeft.Grip) * AxleFront.TireLeft.ActiveWeight;
		AxleFront.TireRight.FrictionForce = Mathf.Clamp(-CornerStiffnessFront * AxleFront.SlipAngle, -AxleFront.TireRight.Grip, AxleFront.TireRight.Grip) * AxleFront.TireRight.ActiveWeight;
		AxleRear.TireLeft.FrictionForce = Mathf.Clamp(-CornerStiffnessRear * AxleRear.SlipAngle, -AxleRear.TireLeft.Grip, AxleRear.TireLeft.Grip) * AxleRear.TireLeft.ActiveWeight;
		AxleRear.TireRight.FrictionForce = Mathf.Clamp(-CornerStiffnessRear * AxleRear.SlipAngle, -AxleRear.TireRight.Grip, AxleRear.TireRight.Grip) * AxleRear.TireRight.ActiveWeight;

	 	// Forces (longitudinal and lateral)
		float tractionForceX = AxleRear.Torque - activeBrake * Mathf.Sign(LocalVelocity.x);
		float tractionForceY = 0;

		float dragForceX = -RollingResistance * LocalVelocity.x - AirResistance * LocalVelocity.x * Mathf.Abs(LocalVelocity.x);
		float dragForceY = -RollingResistance * LocalVelocity.y - AirResistance * LocalVelocity.y * Mathf.Abs(LocalVelocity.y);

		// --- Kinematic vs Dynamic blending
		// dynamicBlend: 0 => pure kinematic bicycle model; 1 => pure dynamic (shoes/tyres)
		float dynamicBlend = Mathf.Clamp01((AbsoluteVelocity - KinematicModeSpeed) / KinematicBlendRange);
		debugDynamicBlend = dynamicBlend;

		// Compute kinematic yaw rate (bicycle model): yawRate = v / L * tan(delta)
		// Use forward speed (local x). If near zero, yawRate small automatically.
		float yawRateKinematic = 0f;
		if (Mathf.Abs(LocalVelocity.x) > 0.01f) {
			yawRateKinematic = (LocalVelocity.x / WheelBase) * Mathf.Tan (SteerAngle);
		}
		debugYawRateKinematic = yawRateKinematic;

		// Average lateral friction per axle, then scale by blend
		float frontFrictionAvg = (AxleFront.TireLeft.FrictionForce + AxleFront.TireRight.FrictionForce) * 0.5f * dynamicBlend;
		float rearFrictionAvg = (AxleRear.TireLeft.FrictionForce + AxleRear.TireRight.FrictionForce) * 0.5f * dynamicBlend;

		float totalForceY = dragForceY + tractionForceY + Mathf.Cos (SteerAngle) * frontFrictionAvg + rearFrictionAvg;

		// adjust Y force so it levels out the car heading at high speeds
		if (AbsoluteVelocity > 10) {
			totalForceY *= (AbsoluteVelocity + 1) / (21f - SpeedTurningStability);
		}

		// If we are not pressing gas, add artificial drag - helps with simulation stability
		if (Throttle == 0) {
			Velocity = Vector2.Lerp (Velocity, Vector2.zero, 0.005f);
		}
	
		// Acceleration
		LocalAcceleration.x = totalForceX / Rigidbody2D.mass;
		LocalAcceleration.y = totalForceY / Rigidbody2D.mass;

		Acceleration.x = cos * LocalAcceleration.x - sin * LocalAcceleration.y;
		Acceleration.y = sin * LocalAcceleration.x + cos * LocalAcceleration.y;

		// Velocity and speed
		Velocity.x += Acceleration.x * Time.deltaTime;
		Velocity.y += Acceleration.y * Time.deltaTime;

		AbsoluteVelocity = Velocity.magnitude;

		// Angular torque of car - use the *scaled* axle lateral forces (frontFrictionAvg, rearFrictionAvg)
		float angularTorque = (frontFrictionAvg * AxleFront.DistanceToCG) - (rearFrictionAvg * AxleRear.DistanceToCG);

		// Compute dynamic angular acceleration and update AngularVelocity
		var angularAcceleration = angularTorque / Inertia;
		AngularVelocity += angularAcceleration * Time.deltaTime;

		// Blend angular velocity between kinematic yaw rate and dynamic AngularVelocity
		AngularVelocity = Mathf.Lerp(yawRateKinematic, AngularVelocity, dynamicBlend);

		// Avoid high computed angular velocities at very low speeds by damping (not hard zero)
		if (AbsoluteVelocity < 0.5f && activeThrottle == 0) {
			// demp small residual motion instead of chopping to zero
			Velocity = Vector2.Lerp(Velocity, Vector2.zero, 5f * Time.deltaTime);
			AngularVelocity = Mathf.Lerp(AngularVelocity, 0f, 5f * Time.deltaTime);
			Acceleration = Vector2.Lerp(Acceleration, Vector2.zero, 5f * Time.deltaTime);
		}

		HeadingAngle += AngularVelocity * Time.deltaTime;
		Rigidbody2D.velocity = Velocity;

		Rigidbody2D.MoveRotation (Mathf.Rad2Deg * HeadingAngle - 90);
	}

	float SmoothSteering(float steerInput) {

		float steer = 0;

		if(Mathf.Abs(steerInput) > 0.001f) {
			steer = Mathf.Clamp(SteerDirection + steerInput * Time.deltaTime * SteerSpeed, -1.0f, 1.0f); 
		}
		else
		{
			if (SteerDirection > 0) {
				steer = Mathf.Max(SteerDirection - Time.deltaTime * SteerAdjustSpeed, 0);
			}
			else if (SteerDirection < 0) {
				steer = Mathf.Min(SteerDirection + Time.deltaTime * SteerAdjustSpeed, 0);
			}
		}

		return steer;
	}

	float SpeedAdjustedSteering(float steerInput) {
		float activeVelocity = Mathf.Min(AbsoluteVelocity, 250.0f);
		float steer = steerInput * (1.0f - (AbsoluteVelocity / SpeedSteerCorrection));
		return steer;
	}

	void OnGUI (){
        if (IsPlayerControlled)
        {
            GUI.Label(new Rect(5, 5, 300, 20), "Speed: " + SpeedKilometersPerHour.ToString());
            GUI.Label(new Rect(5, 25, 300, 20), "RPM: " + Engine.GetRPM(Rigidbody2D).ToString());
            GUI.Label(new Rect(5, 45, 300, 20), "Gear: " + (Engine.CurrentGear + 1).ToString());
            GUI.Label(new Rect(5, 65, 300, 20), "LocalAcceleration: " + LocalAcceleration.ToString());
            GUI.Label(new Rect(5, 85, 300, 20), "Acceleration: " + Acceleration.ToString());
            GUI.Label(new Rect(5, 105, 300, 20), "LocalVelocity: " + LocalVelocity.ToString());
            GUI.Label(new Rect(5, 125, 300, 20), "Velocity: " + Velocity.ToString());
            GUI.Label(new Rect(5, 145, 300, 20), "SteerAngle: " + SteerAngle.ToString());
            GUI.Label(new Rect(5, 165, 300, 20), "Throttle: " + Throttle.ToString());
            GUI.Label(new Rect(5, 185, 300, 20), "Brake: " + Brake.ToString());

            GUI.Label(new Rect(5, 205, 300, 20), "HeadingAngle: " + HeadingAngle.ToString());
            GUI.Label(new Rect(5, 225, 300, 20), "AngularVelocity: " + AngularVelocity.ToString());

            GUI.Label(new Rect(5, 245, 300, 20), "TireFL Weight: " + AxleFront.TireLeft.ActiveWeight.ToString());
            GUI.Label(new Rect(5, 265, 300, 20), "TireFR Weight: " + AxleFront.TireRight.ActiveWeight.ToString());
            GUI.Label(new Rect(5, 285, 300, 20), "TireRL Weight: " + AxleRear.TireLeft.ActiveWeight.ToString());
            GUI.Label(new Rect(5, 305, 300, 20), "TireRR Weight: " + AxleRear.TireRight.ActiveWeight.ToString());

            GUI.Label(new Rect(5, 325, 300, 20), "TireFL Friction: " + AxleFront.TireLeft.FrictionForce.ToString());
            GUI.Label(new Rect(5, 345, 300, 20), "TireFR Friction: " + AxleFront.TireRight.FrictionForce.ToString());
            GUI.Label(new Rect(5, 365, 300, 20), "TireRL Friction: " + AxleRear.TireLeft.FrictionForce.ToString());
            GUI.Label(new Rect(5, 385, 300, 20), "TireRR Friction: " + AxleRear.TireRight.FrictionForce.ToString());

            GUI.Label(new Rect(5, 405, 300, 20), "TireFL Grip: " + AxleFront.TireLeft.Grip.ToString());
            GUI.Label(new Rect(5, 425, 300, 20), "TireFR Grip: " + AxleFront.TireRight.Grip.ToString());
            GUI.Label(new Rect(5, 445, 300, 20), "TireRL Grip: " + AxleRear.TireLeft.Grip.ToString());
            GUI.Label(new Rect(5, 465, 300, 20), "TireRR Grip: " + AxleRear.TireRight.Grip.ToString());

            GUI.Label(new Rect(5, 485, 300, 20), "AxleF SlipAngle: " + AxleFront.SlipAngle.ToString());
            GUI.Label(new Rect(5, 505, 300, 20), "AxleR SlipAngle: " + AxleRear.SlipAngle.ToString());

            GUI.Label(new Rect(5, 525, 300, 20), "AxleF Torque: " + AxleFront.Torque.ToString());
            GUI.Label(new Rect(5, 545, 300, 20), "AxleR Torque: " + AxleRear.Torque.ToString());
        }
	}

	// Debug draw: slip vectors, velocity and blend indicator
	void OnDrawGizmosSelected() {
		if (AxleFront == null || AxleRear == null || Rigidbody2D == null) return;

		// Wheel positions
		Vector3 fl = AxleFront.TireLeft.transform.position;
		Vector3 fr = AxleFront.TireRight.transform.position;
		Vector3 rl = AxleRear.TireLeft.transform.position;
		Vector3 rr = AxleRear.TireRight.transform.position;

		// Draw velocity vector (world)
		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(transform.position, transform.position + (Vector3)Rigidbody2D.velocity);

		// Draw slip force arrows (scaled)
		float scale = 0.0005f;
		Gizmos.color = Color.red;
		Gizmos.DrawLine(fl, fl + transform.up * AxleFront.TireLeft.FrictionForce * scale);
		Gizmos.DrawLine(fr, fr + transform.up * AxleFront.TireRight.FrictionForce * scale);
		Gizmos.DrawLine(rl, rl + transform.up * AxleRear.TireLeft.FrictionForce * scale);
		Gizmos.DrawLine(rr, rr + transform.up * AxleRear.TireRight.FrictionForce * scale);

		// Draw forward vector at CG
		Gizmos.color = Color.green;
		Gizmos.DrawLine(transform.position, transform.position + transform.up * 1.0f);

		#if UNITY_EDITOR
		UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"blend: {debugDynamicBlend:F2}  yawK: {debugYawRateKinematic:F3}");
		#endif
	}

}

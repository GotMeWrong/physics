using UnityEngine;
using System.Collections;

public class Engine : MonoBehaviour {
	
	[SerializeField]
	int[] TorqueCurve = new int[8] { 100, 280, 325, 420, 460, 340, 300, 100 };

	[SerializeField]
	float[] GearRatios = new float[] { 5.8f, 4.5f, 3.74f, 2.8f, 1.6f, 0.79f, 4.2f };

	public int CurrentGear { get; private set; }

	// --- new explicit fields
	[SerializeField]
	float WheelRadius = 0.5f; // m, можно устанавливать через инспектор или брать из Tire

	[SerializeField]
	float FinalDrive = 4.2f; // если у тебя последний элемент GearRatios был final drive, вынеси его сюда

	[SerializeField]
	float RPMLimit = 7500f; // простой limiter, можно доработать

	public float GearRatio {
		get { return GearRatios[CurrentGear]; }
	}

	public float EffectiveGearRatio {
		get { return FinalDrive; }
	}

	public void ShiftUp() {
		// Защищаем выход за границы массива
		CurrentGear = Mathf.Min(CurrentGear + 1, GearRatios.Length - 2);
	}

	public void ShiftDown() {
		CurrentGear = Mathf.Max(CurrentGear - 1, 0);
	}

	public float GetTorque(Rigidbody2D rb) {
		return GetTorque(GetRPM (rb));
	}

	public float GetRPM(Rigidbody2D rb) {
		// Правильный расчёт RPM: v / (2*pi*r) [rev/sec] -> *60 = rpm, затем * gear * finalDrive
		float v = rb.velocity.magnitude; // m/s
		if (WheelRadius <= 0.0001f) WheelRadius = 0.5f;
		float wheelRevsPerSec = v / (2f * Mathf.PI * WheelRadius); // об/сек
		float rpm = wheelRevsPerSec * 60f * GearRatio * EffectiveGearRatio;
		return rpm;
	}

	public float GetTorque(float rpm)
	{        
		if (rpm < 1000) {            
			return Mathf.Lerp (TorqueCurve [0], TorqueCurve [1], rpm / 1000f);
		} else if (rpm < 2000) {
			return Mathf.Lerp (TorqueCurve [1], TorqueCurve [2], (rpm - 1000) / 1000f);
		} else if (rpm < 3000) {
			return Mathf.Lerp (TorqueCurve [2], TorqueCurve [3], (rpm - 2000) / 1000f);
		} else if (rpm < 4000) {
			return Mathf.Lerp (TorqueCurve [3], TorqueCurve [4], (rpm - 3000) / 1000f);
		} else if (rpm < 5000) {
			return Mathf.Lerp (TorqueCurve [4], TorqueCurve [5], (rpm - 4000) / 1000f);
		} else if (rpm < 6000) {
			return Mathf.Lerp (TorqueCurve [5], TorqueCurve [6], (rpm - 5000) / 1000f);
		} else if (rpm < 7000) {
			return Mathf.Lerp (TorqueCurve [6], TorqueCurve [7], (rpm - 6000) / 1000f);
		} else {            
			// Простая логика: выше 7000 — даём сниженный крутящий момент (rev limiter effect)
			return TorqueCurve [6] * 0.25f;
		}

	}

	public void UpdateAutomaticTransmission(Rigidbody2D rb) {
		float rpm = GetRPM (rb);

		if (rpm > 6200) {
			if (CurrentGear < GearRatios.Length - 2)
				CurrentGear++;
		} else if (rpm < 2000) {
			if (CurrentGear > 0)
				CurrentGear--;
		}
	}


}

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using SocketIO;
using UnityStandardAssets.Vehicles.Car;
using System;
using System.Security.AccessControl;

public class CommandServer : MonoBehaviour
{
	public CarRemoteControl CarRemoteControl;
	public Camera FrontFacingCamera;
	private SocketIOComponent _socket;
	private CarController _carController;

	private const string TRACK_TAG = "Track";

	// Use this for initialization
	void Start()
	{
		_socket = GameObject.Find("SocketIO").GetComponent<SocketIOComponent>();
		_socket.On("open", OnOpen);
		_socket.On("steer", OnSteer);
		_socket.On("manual", OnManual);
		_carController = CarRemoteControl.GetComponent<CarController>();
	}

	// Update is called once per frame
	void Update()
	{
	}

	void OnOpen(SocketIOEvent obj)
	{
		Debug.Log("Connection Open");
		EmitTelemetry(obj);
	}

	// 
	void OnManual(SocketIOEvent obj)
	{
		EmitTelemetry(obj);
	}

	void OnSteer(SocketIOEvent obj)
	{
		JSONObject jsonObject = obj.data;
		CarRemoteControl.SteeringAngle = float.Parse(jsonObject.GetField("steering_angle").str);
		CarRemoteControl.Acceleration = float.Parse(jsonObject.GetField("throttle").str);
		EmitTelemetry(obj);
	}

	void EmitTelemetry(SocketIOEvent obj)
	{
		UnityMainThreadDispatcher.Instance().Enqueue(() =>
		{
			// do the ray casting from above the car; when the track is initialized and the car
			// is dropped from a higher ground, the car position raycast fails
			Vector3 carPosition = _carController.transform.position + Vector3.up;
			Ray ray = new Ray(carPosition, Vector3.down);

			bool onTrack = false;
			RaycastHit[] hits = Physics.RaycastAll(ray);
			foreach (var hit in hits) {
				if (hit.collider.tag == TRACK_TAG) {
					onTrack = true;
					break;
				}
			}

			#if DEBUG
				if (!onTrack) {
					Debug.DrawRay(ray.origin, ray.direction * 3, Color.red, 5);
					Debug.Log(String.Format("Car is not on track. There are {0} ray hits.", hits.Length));

					foreach (var hit in hits) {
						Debug.Log("Ray hit collider named: " + hit.collider.name);
					}
				}
			#endif

			Dictionary<string, string> data = new Dictionary<string, string>();
			// send only if it's not being manually driven
			// duplicates the logic in UISystem
			if (!Input.GetKey(KeyCode.W) || !Input.GetKey(KeyCode.S)) {
				// Collect Data from the Car
				data["steering_angle"] = _carController.CurrentSteerAngle.ToString("N4");
				data["throttle"] = _carController.AccelInput.ToString("N4");
				data["speed"] = _carController.CurrentSpeed.ToString("N4");
				data["image"] = Convert.ToBase64String(CameraHelper.CaptureFrame(FrontFacingCamera));

				data["ontrack"] = onTrack.ToString();
			}

			_socket.Emit("telemetry", new JSONObject(data));
		});
	}
}
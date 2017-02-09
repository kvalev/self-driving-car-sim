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
			Vector3 carPosition = _carController.transform.position;
			Ray ray = new Ray(carPosition, Vector3.down);

			RaycastHit hitInfo;
			bool hit = Physics.Raycast(ray, out hitInfo);
			bool onTrack = hit && hitInfo.collider.tag == TRACK_TAG;

			print("Attempting to Send...");
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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gravity : MonoBehaviour
{
    Camera _camera;
    public float gravity = 10f;
    public float maxForce = 10f;

    public GameObject lightPrefab;
    DXRCamera cam;

    void Start()
    {
        _camera = GetComponent<Camera>();
        cam = GetComponent<DXRCamera>();
    }

    void Update()
    {
        if(Input.GetMouseButton(0))
        {
            var pos = _camera.ScreenToWorldPoint(Input.mousePosition);
            var rbs = GameObject.FindObjectsOfType<Rigidbody2D>();
            foreach(var rb in rbs)
            {
                var offset = (Vector2)pos - rb.position;
                var force = offset.normalized * Mathf.Min(gravity / offset.sqrMagnitude, maxForce);
                rb.AddForce(force);
            }
        }

        if(Input.GetMouseButton(1))
        {
            var pos = _camera.ScreenToWorldPoint(Input.mousePosition);
            pos.z = 0f;
            var light = Instantiate(lightPrefab, pos, lightPrefab.transform.rotation);
            
            if(!Input.GetKey(KeyCode.LeftShift))
                light.GetComponent<Renderer>().material.SetColor("_Color", Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f) * 1.5f);

            cam._rtas.AddInstance(light.GetComponent<Renderer>());
        }
    }
}

﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadingController : MonoBehaviour
{

    private Shader shader1;
    private Shader shader2;
    public Renderer[] renderers;

    // Start is called before the first frame update
    void Start()
    {
        shader1 = Shader.Find("Standard");
    //    shader2 = Shader.Find("SuperSystems/Wireframe-Transparent-Culled");
    shader2 = Shader.Find("Overdraw");

        renderers = GetComponentsInChildren<Renderer>();

    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SwitchShading();
        }
        
    }

    private void SwitchShading()
    {
        foreach (Renderer r in renderers)
        {
            if (r.materials[0].shader == shader1)
            {
                foreach(Material m in r.materials)
                {
                    m.shader = shader2;
                    m.SetColor("_Color", new Color(0.1f, 0.1f, 0.1f, 0.5f));
                }
            }
            else
            {
                foreach (Material m in r.materials)
                {
                    m.shader = shader1;
                    m.SetColor("_Color", Color.white);
                }
            }
        }
    }
}
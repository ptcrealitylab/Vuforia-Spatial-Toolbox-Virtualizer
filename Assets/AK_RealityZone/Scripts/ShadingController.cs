/**
 * Copyright (c) 2019 Hisham Bedri
 * Copyright (c) 2019-2020 James Hobin
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */
using System.Collections;
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

        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleShading();
        }
    }

    public void ToggleShading()
    {
        var selectShader1 = renderers[0].materials[0].shader == shader1;
        SwitchShading(!selectShader1);
    }

    public void SwitchShading(bool selectShader1)
    {
        foreach (Renderer r in renderers)
        {
            if (!selectShader1)
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

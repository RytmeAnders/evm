﻿using UnityEngine;
using System.Collections;
using System;
using System.IO;

public class EVM : MonoBehaviour {

    //Assign variables
    bool showBS = false;

    WebCamTexture WCT;
    Texture2D snapshot, diffThres, actual;

    Color[] pix, pix2, pixActual;

    int area, xPos, yPos, xPosOld, yPosOld, y4Head, xMin, xMax, yMin, yMax;
    public float threshold, printHertz;
    public int roiOffset, thresholdOffset, pulseOffset;

    //For calculations
    float green = 0;
    int totalPixels = 0;
    float pulse;
    int whitePixels = 0;

    void Start () {

        //Assign textures
        WCT = new WebCamTexture();
        GetComponent<Renderer>().material.mainTexture = WCT;
        WCT.Play();

        snapshot = new Texture2D(WCT.width, WCT.height);
        diffThres = new Texture2D(WCT.width, WCT.height);
        actual = new Texture2D(WCT.width, WCT.height);

        GameObject.Find("Snap").GetComponent<Renderer>().material.mainTexture = snapshot;
        GameObject.Find("DiffThres").GetComponent<Renderer>().material.mainTexture = diffThres;
        GameObject.Find("CubeActual").GetComponent<Renderer>().material.mainTexture = actual;

    }

    void Update() {

        //-------------------- Controls
        //Take snapshot
        if (Input.GetKeyDown(KeyCode.F1)) {
            pix = WCT.GetPixels();
            snapshot.SetPixels(pix);
            snapshot.Apply();
        }

        //Start calculations
        if (Input.GetKeyDown(KeyCode.F2)) {
            showBS = !showBS;
        }

        //Start writing to files
        if (Input.GetKeyDown(KeyCode.F3)) {
            StartCoroutine(Evm_data_full(printHertz));
            StartCoroutine(Evm_data_pulse(printHertz));
            StartCoroutine(Evm_data_graph(printHertz));
        }

        //Stop writing to files
        if (Input.GetKeyDown(KeyCode.F4)) {
            StopAllCoroutines();
        }

        //Change threshold for the Blob
        if (Input.GetKeyDown(KeyCode.F5))
            threshold -= 0.1f;
        if (Input.GetKeyDown(KeyCode.F6))
            threshold += 0.1f;
        //-------------------- Controls End

        if (showBS) {
            //Assign pixels to arrays
            pixActual = WCT.GetPixels();
            pix = WCT.GetPixels();
            pix2 = snapshot.GetPixels();

            // Absolute difference between snapshot and live video
            for (int i = 0; i < pix.Length; i++) {
                pix2[i].r = Mathf.Abs(pix[i].r - pix2[i].r);
                pix2[i].g = Mathf.Abs(pix[i].g - pix2[i].g);
                pix2[i].b = Mathf.Abs(pix[i].b - pix2[i].b);
            }

            //diffABS.SetPixels(pix2);
            //diffABS.Apply();
            // END ABSOLUTE DIFFERENCE --------------------------

            //

            // THRESHOLD ----------------------------------------
            for (int y = 0; y < WCT.height; y++) {
                for (int x = 0; x < WCT.width; x++) {
                    int i = y * WCT.width + x;
                    if (pix2[i].r + pix2[i].g + pix2[i].b > threshold && y > thresholdOffset) {
                        pix2[i] = Color.white;
                    }
                    else {
                        pix2[i] = Color.black;
                    }
                }
            }
        }
        // CALCULATE CENTER OF MASS -------------------------------------------

        xPosOld = xPos;
        yPosOld = yPos;

        area = 0;
        xPos = 0;
        yPos = 0;

        for (int i = 0; i < pix.Length; i++) {
            if (pix2[i] == Color.white) {
                area++;
                xPos += i % WCT.width;
                yPos += i / WCT.width;
            }
        }

        xPos /= area;
        yPos /= area;

        // DRAWING -----------------------------------------------------
        y4Head = yPos + roiOffset;
        xMin = xPos - 50;
        xMax = xPos + 50;
        yMin = y4Head - 25;
        yMax = y4Head + 25;

        //Draw the center of mass y-line
        for (int y = 0; y < WCT.height; y++) {
            pix2[y * WCT.width + xPos] = Color.green;
        }
        //Draw the center of mass x-line
        for (int x = 0; x < WCT.width; x++) {
            pix2[y4Head * WCT.width + x] = Color.green;
        }

        //Draw AOI bounding box y-line
        for (int y = 1; y < WCT.height - 1; y++) {
            for (int x = 1; x < WCT.width - 1; x++) {
                if (x > xMin && x < xMax && y == yMin)
                    pix2[y * WCT.width + x] = Color.green;
                if (x > xMin && x < xMax && y == yMax)
                    pix2[y * WCT.width + x] = Color.green;
                if (y > yMin && y < yMax && x == xMin)
                    pix2[y * WCT.width + x] = Color.green;
                if (y > yMin && y < yMax && x == xMax)
                    pix2[y * WCT.width + x] = Color.green;
            }
        }

        //Debug.Log("AOI - xMin: " + xMin + " xMax: " + xMax + " yMin: " + yMin + " yMax: " + yMax);

        diffThres.SetPixels(pix2);
        diffThres.Apply();

        // DRAWING END -------------------------------------------------

        //

        //Normalize the green channel within the ROI
        for (int y = yMin; y < yMax; y++) {
            for (int x = xMin; x < xMax; x++) {
                int i = y * WCT.width + x;
                float sum = pix[i].r + pix[i].g + pix[i].b + 0.00001f;
                pix[i].g /= sum;
            }
        }

        // Calculate mean green value of the ROI -----------------------
        //For the pulse algorithm
        green = 0;
        totalPixels = 0;
        whitePixels = 0;

        for (int y = yMin; y < yMax; y++) {
            for (int x = xMin; x < xMax; x++) {
                green += pix[y * WCT.width + x].g;
                totalPixels++;
                if (pix2[y * WCT.width + x] == Color.white) {
                    whitePixels++;
                }
            }
        }

        green /= totalPixels;
        pulse = pulseOffset * green;
        Debug.Log(Time.time.ToString() + " | Compactness: " + whitePixels + "/" + totalPixels + ", Threshold: " + threshold + " || AvgGreen Frame: " + green + " | Pulse: " + pulse);
        // Calculate END -----------------------------------------------
    }

    //StreamWriting
    public void savePreset_full(string doc) {
        using (StreamWriter writeFull = File.AppendText(doc)) {
            writeFull.Write(Time.time.ToString() + " | Compactness: " + whitePixels + "/" + totalPixels + ", Threshold: " + threshold + " || AvgGreen Frame: " + green + " | Pulse: " + pulse + Environment.NewLine);
            writeFull.Close();
        }
    }

    public void savePreset_pulse(string doc) {
        using (StreamWriter writePulse = new StreamWriter(doc)) {
            writePulse.Write(pulse + Environment.NewLine);
            writePulse.Close();
        }
    }

    public void savePreset_graph(string doc) {
        using (StreamWriter writeGraph = File.AppendText(doc))
        {
            writeGraph.Write(pulse + Environment.NewLine);
            writeGraph.Close();
        }
    }

    private IEnumerator Evm_data_full(float waitTime) {
        while (true)
        {
            yield return new WaitForSeconds(waitTime/2);
            savePreset_full(@"D:\evm_data_full.txt");
            Debug.Log("Printing evm_data_full");
        }
    }

    private IEnumerator Evm_data_pulse(float waitTime) {
        while (true) {
            yield return new WaitForSeconds(waitTime);
            savePreset_pulse(@"D:\evm_data_pulse.txt");
            Debug.Log("Printing evm_data_pulse");
        }
    }

    private IEnumerator Evm_data_graph(float waitTime) {
        while (true) {
            yield return new WaitForSeconds(0.015625f);
            savePreset_graph(@"D:\evm_data_graph.txt");
            Debug.Log("Printing evm_data_graph");
        }
    }
}

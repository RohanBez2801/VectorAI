// Vector.Native.cpp - OMNI-COMPATIBLE VERSION (DX12 Friendly)
#include "pch.h"
#include <d3d11.h>
#include <d3dcompiler.h> 
#include <DirectXMath.h>
#include <vector>
#include <cmath>

#pragma comment(lib, "d3d11.lib")

using namespace DirectX;

#define DLLEXPORT extern "C" __declspec(dllexport)

// --- CONSTANTS ---
const int WIDTH = 300;
const int HEIGHT = 300;
const int POINT_COUNT = 900; 

// --- STRUCTS ---
struct Vertex {
    XMFLOAT3 Pos;
    XMFLOAT4 Color;
    float Region; // 0=Skin, 1=Eye, 2=Mouth
};

struct ConstantBuffer {
    XMMATRIX WorldViewProj;
    float Time;
    float BlinkFactor;
    float MouthFactor;
    float SpikeIntensity; 
    XMFLOAT4 MoodColor;    
    float ConfusionFactor; 
    float Padding;
};

// --- GLOBALS ---
ID3D11Device* g_Device = nullptr;
ID3D11DeviceContext* g_Context = nullptr;
ID3D11Texture2D* g_RenderTarget = nullptr;
ID3D11RenderTargetView* g_RTV = nullptr;
ID3D11Texture2D* g_StagingTexture = nullptr;
ID3D11Buffer* g_VertexBuffer = nullptr;
ID3D11Buffer* g_ConstantBuffer = nullptr;
ID3D11InputLayout* g_InputLayout = nullptr;
ID3D11VertexShader* g_VS = nullptr;
ID3D11PixelShader* g_PS = nullptr;
ID3D11BlendState* g_BlendState = nullptr;

// --- STATE ---
XMFLOAT4 g_CurrentMoodColor = { 0.0f, 1.0f, 1.0f, 1.0f }; 
float g_CurrentSpike = 0.0f;
float g_TargetSpike = 0.0f;
XMFLOAT4 g_TargetMoodColor = { 0.0f, 1.0f, 1.0f, 1.0f };

// --- SHADER (HLSL) ---
const char* SHADER_SOURCE = R"(
cbuffer ConstantBuffer : register(b0) {
    matrix WorldViewProj;
    float Time;
    float Blink;
    float Mouth;
    float Spike;
    float4 MoodColor;
    float Confusion;
    float Padding;
};

struct VS_INPUT {
    float3 Pos : POSITION;
    float4 Color : COLOR;
    float Region : TEXCOORD0;
};

struct PS_INPUT {
    float4 Pos : SV_POSITION;
    float4 Color : COLOR;
};

float random(float3 st) { 
    return frac(sin(dot(st, float3(12.9898, 78.233, 45.164))) * 43758.5453123); 
}

PS_INPUT VS(VS_INPUT input) {
    PS_INPUT output;
    float3 pos = input.Pos;
    
    // 0. HEARTBEAT (Alive Pulse - Proves Shader is Running)
    float heartbeat = sin(Time * 3.0) * 2.0;
    pos += normalize(pos) * heartbeat;

    // 1. EMOTION: Spike Effect
    if(Spike > 0.01) {
        float noiseVal = random(pos * (1.0 + Time * 0.1)); 
        pos += normalize(pos) * (noiseVal * Spike * 15.0);
    }

    // 2. MORPH: Blink & Mouth
    if(input.Region > 0.9 && input.Region < 1.1) { 
        pos.y *= (1.0 - Blink * 0.9); 
    }
    if(input.Region > 1.9) { 
        float dist = abs(pos.x);
        if(dist < 20.0) pos.y -= Mouth * (20.0 - dist) * 0.5;
    }

    output.Pos = mul(float4(pos, 1.0), WorldViewProj);
    
    // 3. COLOR MIXING
    if(input.Region > 0.1) {
        output.Color = input.Color; 
    } else {
        output.Color = lerp(input.Color, MoodColor, 0.85);
    }
    
    return output;
}

float4 PS(PS_INPUT input) : SV_Target {
    return input.Color;
}
)";

// --- DYNAMIC COMPILER ---
typedef HRESULT(WINAPI* pD3DCompile)(LPCVOID, SIZE_T, LPCSTR, const D3D_SHADER_MACRO*, ID3DInclude*, LPCSTR, LPCSTR, UINT, UINT, ID3DBlob**, ID3DBlob**);

HRESULT CompileShader(const char* source, const char* entryPoint, const char* target, ID3DBlob** blob) {
    static pD3DCompile D3DCompileFn = nullptr;
    if (!D3DCompileFn) {
        HMODULE hMod = LoadLibrary(L"d3dcompiler_47.dll");
        if (hMod) D3DCompileFn = (pD3DCompile)GetProcAddress(hMod, "D3DCompile");
    }
    if (!D3DCompileFn) return E_FAIL;
    return D3DCompileFn(source, strlen(source), nullptr, nullptr, nullptr, entryPoint, target, D3DCOMPILE_ENABLE_STRICTNESS, 0, blob, nullptr);
}

// --- INIT ---
DLLEXPORT void __stdcall InitVectorEngine(HWND hwnd, int width, int height) {
    if (g_Device) return; 

    // CHANGE: Pass nullptr to FeatureLevels to let DX12/11 auto-negotiate best driver
    D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &g_Device, nullptr, &g_Context);

    if (!g_Device) return; // Safety check

    // Textures
    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = WIDTH;
    desc.Height = HEIGHT;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_RENDER_TARGET;
    g_Device->CreateTexture2D(&desc, nullptr, &g_RenderTarget);
    g_Device->CreateRenderTargetView(g_RenderTarget, nullptr, &g_RTV);

    desc.Usage = D3D11_USAGE_STAGING;
    desc.BindFlags = 0;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    g_Device->CreateTexture2D(&desc, nullptr, &g_StagingTexture);

    // Geometry
    std::vector<Vertex> vertices;
    for (int i = 0; i < POINT_COUNT; i++) {
        float y = 1.0f - (i / (float)(POINT_COUNT - 1)) * 2.0f;
        float radius = sqrt(1.0f - y * y);
        float theta = 2.39996f * i;
        float x = cos(theta) * radius;
        float z = sin(theta) * radius;
        float size = 95.0f;
        Vertex v;
        v.Pos = XMFLOAT3(x * size, y * size * 1.25f, z * size);
        v.Region = 0.0f; 
        v.Color = XMFLOAT4(0.0f, 0.5f, 0.5f, 0.5f); 
        if (y > 0.15f && y < 0.35f && z > 0.4f && abs(x) > 0.15f && abs(x) < 0.5f) { v.Region = 1.0f; v.Color = XMFLOAT4(1.0f, 1.0f, 1.0f, 1.0f); }
        else if (y < -0.15f && y > -0.35f && z > 0.6f && abs(x) < 0.35f) { v.Region = 2.0f; v.Color = XMFLOAT4(0.0f, 1.0f, 0.8f, 1.0f); }
        vertices.push_back(v);
    }

    D3D11_BUFFER_DESC bd = {};
    bd.Usage = D3D11_USAGE_DEFAULT;
    bd.ByteWidth = sizeof(Vertex) * (UINT)vertices.size();
    bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
    D3D11_SUBRESOURCE_DATA initData = { vertices.data(), 0, 0 };
    g_Device->CreateBuffer(&bd, &initData, &g_VertexBuffer);

    bd.ByteWidth = sizeof(ConstantBuffer);
    bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    g_Device->CreateBuffer(&bd, nullptr, &g_ConstantBuffer);

    // Shaders
    ID3DBlob* blob = nullptr;
    if (SUCCEEDED(CompileShader(SHADER_SOURCE, "VS", "vs_5_0", &blob))) {
        g_Device->CreateVertexShader(blob->GetBufferPointer(), blob->GetBufferSize(), nullptr, &g_VS);
        D3D11_INPUT_ELEMENT_DESC ied[] = {
            {"POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
            {"COLOR",    0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0},
            {"TEXCOORD", 0, DXGI_FORMAT_R32_FLOAT, 0, 28, D3D11_INPUT_PER_VERTEX_DATA, 0},
        };
        g_Device->CreateInputLayout(ied, 3, blob->GetBufferPointer(), blob->GetBufferSize(), &g_InputLayout);
        blob->Release();
    }
    if (SUCCEEDED(CompileShader(SHADER_SOURCE, "PS", "ps_5_0", &blob))) {
        g_Device->CreatePixelShader(blob->GetBufferPointer(), blob->GetBufferSize(), nullptr, &g_PS);
        blob->Release();
    }

    // Blend State
    D3D11_BLEND_DESC blendDesc = { 0 };
    blendDesc.RenderTarget[0].BlendEnable = TRUE;
    blendDesc.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_ALPHA;
    blendDesc.RenderTarget[0].DestBlend = D3D11_BLEND_ONE; // Additive
    blendDesc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
    blendDesc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
    blendDesc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
    blendDesc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
    blendDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    g_Device->CreateBlendState(&blendDesc, &g_BlendState);
}

DLLEXPORT void __stdcall UpdateMood(float r, float g, float b, float spike, float confusion) {
    g_TargetMoodColor = XMFLOAT4(r, g, b, 1.0f);
    g_TargetSpike = spike;
}

DLLEXPORT void __stdcall RenderFace(float time, float blink, float mouth, int* outputBuffer) {
    if (!g_Context || !g_ConstantBuffer) return;

    // Smooth
    XMVECTOR curCol = XMLoadFloat4(&g_CurrentMoodColor);
    XMVECTOR tarCol = XMLoadFloat4(&g_TargetMoodColor);
    XMStoreFloat4(&g_CurrentMoodColor, XMVectorLerp(curCol, tarCol, 0.08f));
    g_CurrentSpike += (g_TargetSpike - g_CurrentSpike) * 0.08f;

    // Clear
    float clearColor[] = { 0.0f, 0.0f, 0.0f, 0.0f };
    g_Context->ClearRenderTargetView(g_RTV, clearColor);
    g_Context->OMSetRenderTargets(1, &g_RTV, nullptr);
    
    // --- CRITICAL FIX: FORCE VIEWPORT EVERY FRAME ---
    D3D11_VIEWPORT vp;
    vp.Width = (float)WIDTH;
    vp.Height = (float)HEIGHT;
    vp.MinDepth = 0.0f;
    vp.MaxDepth = 1.0f;
    vp.TopLeftX = 0;
    vp.TopLeftY = 0;
    g_Context->RSSetViewports(1, &vp);
    // ------------------------------------------------

    g_Context->OMSetBlendState(g_BlendState, nullptr, 0xFFFFFFFF);

    // Constants
    ConstantBuffer cb;
    XMMATRIX view = XMMatrixLookAtLH(XMVectorSet(0, 0, -400, 0), XMVectorSet(0, 0, 0, 0), XMVectorSet(0, 1, 0, 0));
    XMMATRIX proj = XMMatrixPerspectiveFovLH(XM_PIDIV4, 1.0f, 1.0f, 1000.0f);
    XMMATRIX world = XMMatrixRotationY(time * 0.5f);
    cb.WorldViewProj = XMMatrixTranspose(world * view * proj);
    cb.Time = time;
    cb.BlinkFactor = blink;
    cb.MouthFactor = mouth;
    cb.SpikeIntensity = g_CurrentSpike;
    cb.MoodColor = g_CurrentMoodColor;
    g_Context->UpdateSubresource(g_ConstantBuffer, 0, nullptr, &cb, 0, 0);

    // Draw
    UINT stride = sizeof(Vertex);
    UINT offset = 0;
    g_Context->IASetVertexBuffers(0, 1, &g_VertexBuffer, &stride, &offset);
    g_Context->IASetInputLayout(g_InputLayout);
    g_Context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_POINTLIST);
    g_Context->VSSetShader(g_VS, nullptr, 0);
    g_Context->VSSetConstantBuffers(0, 1, &g_ConstantBuffer);
    g_Context->PSSetShader(g_PS, nullptr, 0);
    g_Context->Draw(POINT_COUNT, 0);

    // Readback
    g_Context->CopyResource(g_StagingTexture, g_RenderTarget);
    D3D11_MAPPED_SUBRESOURCE mapped;
    if (SUCCEEDED(g_Context->Map(g_StagingTexture, 0, D3D11_MAP_READ, 0, &mapped))) {
        byte* src = (byte*)mapped.pData;
        byte* dst = (byte*)outputBuffer;
        for (int y = 0; y < HEIGHT; y++) {
            memcpy(dst + (y * WIDTH * 4), src + (y * mapped.RowPitch), WIDTH * 4);
        }
        g_Context->Unmap(g_StagingTexture, 0);
    }
}
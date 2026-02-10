#include <Arduino.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <FastLED.h>

// --- CONFIGURAÇÃO DO LED (ATOM MATRIX 5x5) ---
#define PIN_LED    27
#define NUM_LEDS   25
CRGB leds[NUM_LEDS];

// --- CONFIGURAÇÕES BLE ---
#define BLE_SERVER_NAME "AtomMatrix-UART"
#define SERVICE_UUID           "6E400001-B5A3-F393-E0A9-E50E24DCCA9E" 
#define CHARACTERISTIC_UUID_RX "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define CHARACTERISTIC_UUID_TX "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"

// --- CONFIGURAÇÃO DE BUFFER PARA DADOS GRANDES ---
#define MAX_BUFFER_SIZE 4096  // Buffer para chunks grandes
String receiveBuffer = "";
unsigned long lastReceiveTime = 0;
#define BUFFER_TIMEOUT 1000  // 1 segundo de timeout

BLEServer *pServer = NULL;
BLECharacteristic *pTxCharacteristic;
bool deviceConnected = false;
bool oldDeviceConnected = false;

// ========== FUNÇÕES DE LED ==========

void mudarCorTodos(CRGB cor) {
    fill_solid(leds, NUM_LEDS, cor);
    FastLED.show();
}

void mudarCorCentro(CRGB cor) {
    FastLED.clear();
    leds[12] = cor;
    FastLED.show();
}

void desenharBorda(CRGB cor) {
    FastLED.clear();
    for(int i = 0; i < 5; i++) leds[i] = cor;
    for(int i = 20; i < 25; i++) leds[i] = cor;
    leds[5] = cor; leds[9] = cor;
    leds[10] = cor; leds[14] = cor;
    leds[15] = cor; leds[19] = cor;
    FastLED.show();
}

void efeitoProgresso(int progress, int total) {
    // Mostra progresso de 0-24 LEDs
    int ledsOn = map(progress, 0, total, 0, NUM_LEDS);
    FastLED.clear();
    for(int i = 0; i < ledsOn; i++) {
        leds[i] = CRGB::Blue;
    }
    FastLED.show();
}

void efeitoPulso(CRGB cor, int pulsos = 1) {
    for(int p = 0; p < pulsos; p++) {
        for(int brightness = 0; brightness < 255; brightness += 30) {
            fill_solid(leds, NUM_LEDS, cor);
            FastLED.setBrightness(brightness);
            FastLED.show();
            delay(10);
        }
        for(int brightness = 255; brightness > 0; brightness -= 30) {
            FastLED.setBrightness(brightness);
            FastLED.show();
            delay(10);
        }
    }
    FastLED.setBrightness(50);
}

// ========== CALLBACKS BLE ==========

class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
        deviceConnected = true;
        Serial.println(">> BLE: CONECTADO");
        mudarCorTodos(CRGB::Green);
    };
    
    void onDisconnect(BLEServer* pServer) {
        deviceConnected = false;
        Serial.println(">> BLE: DESCONECTADO");
        desenharBorda(CRGB::Blue);
    }
};

class MyCallbacks: public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic *pCharacteristic) {
        uint8_t* pData = pCharacteristic->getData();
        size_t length = pCharacteristic->getValue().length();
      
        if (length > 0) {
            // Adiciona ao buffer byte por byte
            for(size_t i = 0; i < length; i++) {
                receiveBuffer += (char)pData[i];
            }
            lastReceiveTime = millis();
            
            Serial.print(">> BLE RX (");
            Serial.print(length);
            Serial.print(" bytes): ");
            
            // Detecta tipo de dados
            if (receiveBuffer.startsWith("DTC:")) {
                Serial.println("[DTC CHUNK]");
                mudarCorCentro(CRGB::Purple);
            } else if (receiveBuffer.startsWith("PHOTO:")) {
                Serial.println("[PHOTO DATA]");
                mudarCorCentro(CRGB::Cyan);
            } else {
                Serial.println(receiveBuffer);
                mudarCorCentro(CRGB::White);
            }
            
            delay(50);
            
            // Volta à cor de estado
            if(deviceConnected) {
                mudarCorTodos(CRGB::Green);
            } else {
                desenharBorda(CRGB::Blue);
            }
        }
    }
};

// ========== SETUP ==========

void setup() {
    // LED
    FastLED.addLeds<WS2812, PIN_LED, GRB>(leds, NUM_LEDS);
    FastLED.setBrightness(50);
    mudarCorTodos(CRGB::Red);
    
    // Serial
    Serial.begin(115200);
    delay(1000);
    Serial.println("\n\n========================================");
    Serial.println("    ATOM MATRIX - DTC GATEWAY");
    Serial.println("========================================");
    
    // BLE
    Serial.println("Iniciando BLE...");
    BLEDevice::init(BLE_SERVER_NAME);
    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new MyServerCallbacks());
    
    BLEService *pService = pServer->createService(SERVICE_UUID);
    
    // TX Characteristic (Atom → App)
    pTxCharacteristic = pService->createCharacteristic(
        CHARACTERISTIC_UUID_TX,
        BLECharacteristic::PROPERTY_NOTIFY
    );
    pTxCharacteristic->addDescriptor(new BLE2902());
    
    // RX Characteristic (App → Atom)
    BLECharacteristic *pRxCharacteristic = pService->createCharacteristic(
        CHARACTERISTIC_UUID_RX,
        BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR
    );
    pRxCharacteristic->setCallbacks(new MyCallbacks());
    
    pService->start();
    
    // Advertising
    BLEAdvertising *pAdvertising = pServer->getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(true);
    pAdvertising->setMinPreferred(0x06);
    pAdvertising->setMinPreferred(0x12);
    pAdvertising->start();
    
    Serial.println("✓ BLE pronto e aguardando conexões...");
    Serial.println("Nome: " + String(BLE_SERVER_NAME));
    Serial.println("========================================\n");
    
    desenharBorda(CRGB::Blue);
}

// ========== LOOP ==========

void loop() {
    // ===== RECEBE DADOS DO SERIAL (PC → BLE) =====
    if (Serial.available()) {
        String serialData = Serial.readStringUntil('\n');
        serialData.trim();
        
        if (serialData.length() > 0 && deviceConnected) {
            Serial.print("<< SERIAL RX → BLE TX: ");
            Serial.println(serialData);
            
            // Envia via BLE
            pTxCharacteristic->setValue(serialData.c_str());
            pTxCharacteristic->notify();
            
            // Feedback visual
            mudarCorCentro(CRGB::Yellow);
            delay(50);
            if(deviceConnected) mudarCorTodos(CRGB::Green);
        }
    }
    
    // ===== PROCESSA BUFFER DE DADOS RECEBIDOS VIA BLE =====
    if (receiveBuffer.length() > 0) {
        // Se passou o timeout ou recebeu marcador de fim
        if (millis() - lastReceiveTime > BUFFER_TIMEOUT || 
            receiveBuffer.endsWith("\n") || 
            receiveBuffer.endsWith("\r\n")) {
            
            // Envia buffer completo para o Serial (USB → PC)
            Serial.print(">> BLE → SERIAL: ");
            Serial.println(receiveBuffer);
            
            // Limpa buffer
            receiveBuffer = "";
        }
        
        // Proteção contra overflow
        if (receiveBuffer.length() > MAX_BUFFER_SIZE) {
            Serial.println("!! ERRO: Buffer overflow, limpando...");
            receiveBuffer = "";
            efeitoPulso(CRGB::Red, 2);
        }
    }
    
    // ===== GESTÃO DE RECONEXÃO =====
    if (!deviceConnected && oldDeviceConnected) {
        delay(500);
        pServer->startAdvertising();
        Serial.println(">> BLE: Re-anunciando...");
        desenharBorda(CRGB::Blue);
        oldDeviceConnected = deviceConnected;
    }
    
    if (deviceConnected && !oldDeviceConnected) {
        oldDeviceConnected = deviceConnected;
    }
    
    delay(10);  // Pequeno delay para estabilidade
}
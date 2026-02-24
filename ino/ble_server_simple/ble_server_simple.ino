#include <Arduino.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

// --- BLE SETTINGS ---
#define BLE_SERVER_NAME "AtomS3-UART"
#define SERVICE_UUID           "6E400001-B5A3-F393-E0A9-E50E24DCCA9E" 
#define CHARACTERISTIC_UUID_RX "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"
#define CHARACTERISTIC_UUID_TX "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"

BLEServer *pServer = NULL;
BLECharacteristic *pTxCharacteristic;
bool deviceConnected = false;
bool oldDeviceConnected = false;

// --- SERVER CALLBACKS ---
class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      deviceConnected = true;
      Serial.println(">> STATUS: CONNECTED");
    };

    void onDisconnect(BLEServer* pServer) {
      deviceConnected = false;
      Serial.println(">> STATUS: DISCONNECTED");
    }
};

// --- WRITE CALLBACKS ---
class MyCallbacks: public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic *pCharacteristic) {
      // FIX: Added .c_str() to convert std::string to Arduino String
      String rxValue = pCharacteristic->getValue().c_str();

      if (rxValue.length() > 0) {
        Serial.print("RECEIVED VIA BLE: ");
        Serial.println(rxValue);
      }
    }
};

void setup() {
  Serial.begin(115200);
  delay(2000); 
  Serial.println("--- INITIALIZING BLE SERVER ---");

  BLEDevice::init(BLE_SERVER_NAME);
  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());

  BLEService *pService = pServer->createService(SERVICE_UUID);

  pTxCharacteristic = pService->createCharacteristic(
                        CHARACTERISTIC_UUID_TX,
                        BLECharacteristic::PROPERTY_NOTIFY
                      );
  pTxCharacteristic->addDescriptor(new BLE2902());

  BLECharacteristic * pRxCharacteristic = pService->createCharacteristic(
                         CHARACTERISTIC_UUID_RX,
                         BLECharacteristic::PROPERTY_WRITE
                       );
  pRxCharacteristic->setCallbacks(new MyCallbacks());

  pService->start();
  pServer->getAdvertising()->addServiceUUID(SERVICE_UUID);
  pServer->getAdvertising()->start();
  
  Serial.println("Ready. Waiting for a connection...");
}

void loop() {
  // Logic for Sending (Serial -> Phone)
  if (deviceConnected) {
    if (Serial.available()) {
        String txString = Serial.readStringUntil('\n');
        txString.trim(); 
        
        if (txString.length() > 0) {
            Serial.print("SENDING TO BLE: ");
            Serial.println(txString);
            
            // Sending the string safely as a C-style string
            pTxCharacteristic->setValue(txString.c_str());
            pTxCharacteristic->notify();
        }
    }
  }

  // Connection Stability Handler
  if (!deviceConnected && oldDeviceConnected) {
      delay(500); 
      pServer->startAdvertising(); 
      Serial.println("Restarting Advertising...");
      oldDeviceConnected = deviceConnected;
  }
  
  if (deviceConnected && !oldDeviceConnected) {
      oldDeviceConnected = deviceConnected;
  }
}
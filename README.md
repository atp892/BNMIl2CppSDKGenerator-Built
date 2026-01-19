# Il2CppSDKGenerator
A ByNameModding Il2Cpp SDK generator for Android, Built for you.

# How to use
1. Compile Il2CppSDK generator (or use release version)
2. Get your DummyDll from using [Il2CppDumper](https://github.com/Perfare/Il2CppDumper/)
3. Drag the whole folder of DummyDll to Il2CppSDK.exe
4. Wait for your SDK to be generated
5. Copy the result to your project
6. Copy precoded Il2Cpp Headers to your project (And include it to your Android.mk or CMakeList.txt)

Done!

# How to use the SDK
You can include an namespace header to compile whole classes within a namespace or just compile a single class, for example take a look at SDK result.

Please read how to Initialize Il2Cpp Functions first before using any of the SDK/Il2Cpp Functions.

FULL CREDITS TO PUBERT AND EVERYONE WHO WORKED ON THIS BEFORE PUBERT.

**Example:**
```c++
#include "SDK/GameBase.h"
#include "SDK/GameEngine.h"

#include "SDK/Includes/UnityEngine/Component.h"
#include "SDK/Includes/UnityEngine/Transform.h"
#include "SDK/Includes/UnityEngine/Screen.h"
#include "SDK/Includes/UnityEngine/Camera.h"
#include "SDK/Includes/UnityEngine/Physics.h"
#include "SDK/Includes/UnityEngine/RaycastHit.h"
#include "SDK/Includes/UnityEngine/Object.h"

using namespace UnityEngine;
using namespace GameBase;
using namespace GameEngine;

void PrintLocation()
{
  auto baseGame = GamePlay::get_Game<BaseGame*>();
  if (baseGame) {
    auto localPawn = GamePlay::get_LocalPawn<Pawn*>();
      if(localPawn) {
      auto pawnTransform = ((Component *)localPawn)->get_transform<Transform *>();
      LOG("Local Pawn: %f %f %f", pawnTransform->get_position().x, pawnTransform->get_position().y, pawnTransform->get_position().z);
      localPawn->m_Health() = 9999.9f;
    }
  }
}

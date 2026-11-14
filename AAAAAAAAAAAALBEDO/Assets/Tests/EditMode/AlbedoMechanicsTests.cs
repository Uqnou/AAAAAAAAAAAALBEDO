using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class AlbedoMechanicsTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    private GameObject CreateGO(string name)
    {
        var go = new GameObject(name);
        createdObjects.Add(go);
        return go;
    }

    [Test]
    public void AdditiveColorUtility_RedPlusGreenProducesYellow_AndMatchesYellowOnly()
    {
        Color combined = AlbedoColorUtility.CombineAdditive(new[] { Color.red, Color.green });
        Assert.IsTrue(AlbedoColorUtility.ColorsMatch(combined, new Color(1f, 1f, 0f, 1f), true));
        Assert.IsFalse(AlbedoColorUtility.ColorsMatch(combined, Color.white, true));
        Assert.IsFalse(AlbedoColorUtility.ColorsMatch(Color.white, new Color(1f, 1f, 0f, 1f), true));
    }

    [Test]
    public void BarrierSombra_RequiresExactColor_YellowNeedsRedAndGreen()
    {
        GameObject sombraGo = CreateGO("TestBarrierYellow");
        sombraGo.AddComponent<BoxCollider>();
        MeshRenderer mr = sombraGo.AddComponent<MeshRenderer>();
        SombraController sombra = sombraGo.AddComponent<SombraController>();
        sombra.Mode = ShadowMode.Barrier;
        sombra.ConfigureRequiredColor(AlbedoColorPreset.Yellow, exactColor: true);

        GameObject redGo = CreateGO("RedPrism");
        PrismaController redPrism = redGo.AddComponent<PrismaController>();
        redPrism.SetColorPreset(AlbedoColorPreset.Red);

        GameObject greenGo = CreateGO("GreenPrism");
        PrismaController greenPrism = greenGo.AddComponent<PrismaController>();
        greenPrism.SetColorPreset(AlbedoColorPreset.Green);

        // Sem luz emitindo ou com apenas um prisma, barreira amarela permanece fechada
        sombra.SetIlluminated(true, redPrism);
        Assert.IsFalse(sombra.IsHidden);
        Assert.IsFalse(sombraGo.GetComponent<BoxCollider>().isTrigger);

        // Simula feixes ativos usando LightSource e Update via método público de iluminação?
        // Em EditMode sem loop de Update, verificamos a lógica direta de combinação e bloqueio de luz
        GameObject lightGo = CreateGO("TestLight");
        Light unityLight = lightGo.AddComponent<Light>();
        unityLight.type = LightType.Spot;
        LightSource ls = lightGo.AddComponent<LightSource>();
        Assert.IsTrue(ls.IsActive);

        // Verifica cálculo estático de emissão de prisma
        Color redOut = PrismaController.ComputeEmittedColor(redPrism.EffectivePrismColor, Color.white, PrismColorMode.PrismTint);
        Color greenOut = PrismaController.ComputeEmittedColor(greenPrism.EffectivePrismColor, Color.white, PrismColorMode.PrismTint);
        Color mixed = AlbedoColorUtility.CombineAdditive(new[] { redOut, greenOut });

        Assert.IsTrue(AlbedoColorUtility.ColorsMatch(mixed, sombra.EffectiveRequiredColor, true));
        Assert.IsTrue(mr.enabled);
    }

    [Test]
    public void WalkableBridge_IsBrokenWhenLightActive_AndWalkableWhenSuppressedByTorch()
    {
        GameObject lightGo = CreateGO("BridgeLight");
        Light unityLight = lightGo.AddComponent<Light>();
        unityLight.type = LightType.Spot;
        LightSource ls = lightGo.AddComponent<LightSource>();

        GameObject bridgeGo = CreateGO("BridgeSombra");
        BoxCollider bridgeCol = bridgeGo.AddComponent<BoxCollider>();
        MeshRenderer bridgeMr = bridgeGo.AddComponent<MeshRenderer>();
        SombraController bridge = bridgeGo.AddComponent<SombraController>();
        bridge.Mode = ShadowMode.WalkableBridge;
        bridge.RegisterLinkedLight(ls);

        // Com luz ativa, passarela deve ficar quebrada (oculta e não-sólida)
        bridge.ReevaluateState(true);
        Assert.IsFalse(bridge.IsBridgeWalkable);
        Assert.IsTrue(bridge.IsHidden);
        Assert.IsTrue(bridgeCol.isTrigger);
        Assert.IsFalse(bridgeMr.enabled);

        // Tocha de Fogo Negro suprime a luz
        GameObject torchGo = CreateGO("TestTorch");
        TorchController torch = torchGo.AddComponent<TorchController>();
        torch.Ignite();
        ls.SetSuppressed(true, torch);

        Assert.IsTrue(ls.IsSuppressed);
        Assert.IsFalse(ls.IsActive);
        Assert.IsFalse(unityLight.enabled);

        // Reavalia passarela após supressão da luz -> fica sólida e caminhável!
        bridge.ReevaluateState(false);
        Assert.IsTrue(bridge.IsBridgeWalkable);
        Assert.IsFalse(bridge.IsHidden);
        Assert.IsFalse(bridgeCol.isTrigger);
        Assert.IsTrue(bridgeMr.enabled);

        // Ao apagar a tocha (gancho futuro), luz volta e passarela quebra novamente
        ls.SetSuppressed(false, torch);
        Assert.IsTrue(ls.IsActive);
        Assert.IsTrue(unityLight.enabled);
        bridge.ReevaluateState(false);
        Assert.IsFalse(bridge.IsBridgeWalkable);
    }

    [Test]
    public void Brazier_IgnitesTorchOnlyOnceTriggered_AndKeepsItLitPermanently()
    {
        GameObject brazierGo = CreateGO("Brazier");
        BlackFireBrazier brazier = brazierGo.AddComponent<BlackFireBrazier>();

        GameObject torchGo = CreateGO("Torch");
        GameObject flameGo = CreateGO("Flame");
        flameGo.transform.SetParent(torchGo.transform);
        TorchController torch = torchGo.AddComponent<TorchController>();
        torch.SetBlackFireVisual(flameGo);

        Assert.IsFalse(torch.IsLit);
        Assert.IsFalse(flameGo.activeSelf);

        bool ignited = brazier.TryIgniteTorch(torch);
        Assert.IsTrue(ignited);
        Assert.IsTrue(torch.IsLit);
        Assert.IsTrue(flameGo.activeSelf);

        // Segunda tentativa não altera estado nem quebra permanência
        bool secondTry = brazier.TryIgniteTorch(torch);
        Assert.IsFalse(secondTry);
        Assert.IsTrue(torch.IsLit);
    }

    [Test]
    public void PickableObject_RotateY_RotatesAroundWorldUpWithoutTilting()
    {
        GameObject prismGo = CreateGO("PrismHoldTest");
        prismGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        prismGo.AddComponent<Rigidbody>();
        prismGo.AddComponent<BoxCollider>();
        PickableObject pickable = prismGo.AddComponent<PickableObject>();

        GameObject holdGo = CreateGO("HoldPoint");
        pickable.PickUp(holdGo.transform);

        Vector3 dirBefore = prismGo.transform.up;
        pickable.RotateY(90f);
        Vector3 dirAfter = prismGo.transform.up;

        Assert.AreEqual(0f, Vector3.Dot(dirBefore.normalized, dirAfter.normalized), 0.05f);
        pickable.Drop(Vector3.zero);
        Assert.IsFalse(pickable.IsPickedUp);
    }

    [Test]
    public void LevelExitButton_TriggersCompletionEventOnce()
    {
        GameObject exitGo = CreateGO("ExitButton");
        exitGo.AddComponent<BoxCollider>();
        LevelExitButton button = exitGo.AddComponent<LevelExitButton>();

        int completedCount = 0;
        button.onLevelCompleted.AddListener(() => completedCount++);

        button.ActivateExit();
        button.ActivateExit();

        Assert.IsTrue(button.IsCompleted);
        Assert.AreEqual(1, completedCount);
    }
}

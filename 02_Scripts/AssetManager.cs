using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetManager
{
    #region Sprite
    private static Dictionary<string/*AddressableName*/, AsyncOperationHandle<Sprite>> m_SpriteOperationHandlesByAddressableName = new();

    public static Sprite LoadSprite(string addressableName)
    {
        if (m_SpriteOperationHandlesByAddressableName.TryGetValue(addressableName, out var handle))
        {
            if (handle.IsDone)
                return handle.Result;

            return handle.WaitForCompletion();
        }

        handle = Addressables.LoadAssetAsync<Sprite>(addressableName);
        m_SpriteOperationHandlesByAddressableName.Add(addressableName, handle);
        return handle.WaitForCompletion();
    }

    public static void ReleaseSprite(string addressableName)
    {
        if (m_SpriteOperationHandlesByAddressableName.TryGetValue (addressableName, out var handle))
        {
            m_SpriteOperationHandlesByAddressableName.Remove(addressableName);
            Addressables.Release(handle);
        }
    }
    #endregion

    #region GameObject
    private static Dictionary<string/*AddressableName*/, AsyncOperationHandle<GameObject>> m_GameObjectOperationHandlesByAddressableName = new();

    public static GameObject LoadGameObject(string addressableName)
    {
        if (m_GameObjectOperationHandlesByAddressableName.TryGetValue(addressableName, out var handle))
        {
            if (handle.IsDone)
                return handle.Result;

            return handle.WaitForCompletion();
        }

        handle = Addressables.InstantiateAsync(addressableName);
        m_GameObjectOperationHandlesByAddressableName.Add(addressableName, handle);
        return handle.WaitForCompletion();
    }

    public static void ReleaseGameObject(string addressableName)
    {
        if (m_GameObjectOperationHandlesByAddressableName.TryGetValue(addressableName, out var handle))
        {
            m_GameObjectOperationHandlesByAddressableName.Remove(addressableName);
            Addressables.Release(handle);
        }
    }
    #endregion
}

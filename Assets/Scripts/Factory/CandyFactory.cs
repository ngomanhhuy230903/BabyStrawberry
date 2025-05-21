// CandyFactory.cs
using UnityEngine;
using System.Collections.Generic;

public class CandyFactory
{
    private readonly Dictionary<string, ObjectPool> _candyPools = new Dictionary<string, ObjectPool>();
    private readonly Transform _candyParentTransform;
    private readonly int _initialPoolSizePerCandyType;

    // Giữ lại prefab arrays để khởi tạo pool
    private readonly GameObject[] _regularCandyPrefabs;
    private readonly GameObject[] _rowClearerPrefabs;
    private readonly GameObject[] _columnClearerPrefabs;

    public CandyFactory(GameObject[] regularCandyPrefabs, GameObject[] rowClearerPrefabs, GameObject[] columnClearerPrefabs, Transform candyParentTransform, int initialPoolSize = 10)
    {
        _regularCandyPrefabs = regularCandyPrefabs;
        _rowClearerPrefabs = rowClearerPrefabs;
        _columnClearerPrefabs = columnClearerPrefabs;
        _candyParentTransform = candyParentTransform;
        _initialPoolSizePerCandyType = initialPoolSize > 0 ? initialPoolSize : 1; // Đảm bảo pool size > 0

        // Validation cơ bản (giữ nguyên)
        if (_regularCandyPrefabs == null || _regularCandyPrefabs.Length == 0)
            Debug.LogError("CandyFactory Error: Regular candy prefabs array is null or empty.");
        // ... (các validation khác) ...
        if (_candyParentTransform == null)
            Debug.LogError("CandyFactory Error: Candy parent transform is null.");

        InitializePools();
    }

    private string GetPoolKey(GameObject prefab)
    {
        if (prefab == null) return "NULL_PREFAB_KEY";
        return prefab.name; // Sử dụng tên prefab làm key cho pool
    }

    private void InitializePools()
    {
        if (_regularCandyPrefabs == null) return;

        for (int i = 0; i < _regularCandyPrefabs.Length; i++)
        {
            // Pool cho kẹo thường
            if (_regularCandyPrefabs[i] != null)
            {
                string key = GetPoolKey(_regularCandyPrefabs[i]);
                if (!_candyPools.ContainsKey(key))
                {
                    _candyPools[key] = new ObjectPool(_regularCandyPrefabs[i], _initialPoolSizePerCandyType, _candyParentTransform);
                    Debug.Log($"Initialized pool for {key} with size {_initialPoolSizePerCandyType}");
                }
            }
            else Debug.LogError($"CandyFactory: Regular candy prefab at index {i} is null.");

            // Pool cho kẹo xóa hàng (nếu có)
            if (i < _rowClearerPrefabs.Length && _rowClearerPrefabs[i] != null)
            {
                string key = GetPoolKey(_rowClearerPrefabs[i]);
                if (!_candyPools.ContainsKey(key))
                {
                    _candyPools[key] = new ObjectPool(_rowClearerPrefabs[i], _initialPoolSizePerCandyType / 2 > 0 ? _initialPoolSizePerCandyType / 2 : 1, _candyParentTransform);
                    Debug.Log($"Initialized pool for {key} with size {_initialPoolSizePerCandyType / 2}");
                }
            }
            else if (i < _rowClearerPrefabs.Length) Debug.LogError($"CandyFactory: Row clearer prefab at index {i} is null.");


            // Pool cho kẹo xóa cột (nếu có)
            if (i < _columnClearerPrefabs.Length && _columnClearerPrefabs[i] != null)
            {
                string key = GetPoolKey(_columnClearerPrefabs[i]);
                if (!_candyPools.ContainsKey(key))
                {
                    _candyPools[key] = new ObjectPool(_columnClearerPrefabs[i], _initialPoolSizePerCandyType / 2 > 0 ? _initialPoolSizePerCandyType / 2 : 1, _candyParentTransform);
                    Debug.Log($"Initialized pool for {key} with size {_initialPoolSizePerCandyType / 2}");
                }
            }
            else if (i < _columnClearerPrefabs.Length) Debug.LogError($"CandyFactory: Column clearer prefab at index {i} is null.");
        }
        Debug.Log($"CandyFactory: Object pools initialization complete. Total pools: {_candyPools.Count}");
    }

    private Candy GetCandyFromPool(GameObject prefab, CandyType typeForInit, int xIndex, int yIndex, Vector3 position, bool isSpecial, SpecialCandyEffect effect)
    {
        if (prefab == null)
        {
            Debug.LogError($"CandyFactory: Prefab is null. Cannot create candy of type {typeForInit}.");
            return null;
        }

        string poolKey = GetPoolKey(prefab);
        if (!_candyPools.ContainsKey(poolKey) || _candyPools[poolKey] == null)
        {
            Debug.LogError($"CandyFactory: No pool for prefab '{prefab.name}' (key: {poolKey}). Creating new pool on-the-fly or ensure it's pre-initialized.");
            // Fallback: Tạo pool mới nếu chưa có (không khuyến khích cho production, nên pre-init hết)
            _candyPools[poolKey] = new ObjectPool(prefab, _initialPoolSizePerCandyType / 2 > 0 ? _initialPoolSizePerCandyType / 2 : 1, _candyParentTransform);
            if (!_candyPools.ContainsKey(poolKey))
            { // Nếu vẫn không tạo được pool
                Debug.LogError($"CandyFactory: Failed to create on-the-fly pool for '{prefab.name}'.");
                return null;
            }
        }

        GameObject candyGO = _candyPools[poolKey].GetObject();
        if (candyGO == null)
        {
            Debug.LogError($"CandyFactory: Pool for '{prefab.name}' returned null object.");
            return null;
        }

        candyGO.transform.position = position;
        candyGO.transform.rotation = Quaternion.identity;
        // Parent đã được set bởi pool, nhưng có thể set lại ở đây nếu cần đảm bảo
        if (_candyParentTransform != null && candyGO.transform.parent != _candyParentTransform)
        {
            candyGO.transform.SetParent(_candyParentTransform);
        }


        Candy candyComponent = candyGO.GetComponent<Candy>();
        if (candyComponent == null)
        {
            Debug.LogError($"CandyFactory: Pooled prefab '{prefab.name}' is missing Candy component. Returning to pool.");
            _candyPools[poolKey].ReturnObject(candyGO);
            return null;
        }

        candyComponent.Init(xIndex, yIndex, typeForInit, isSpecial, effect);
        return candyComponent;
    }

    public Candy CreateRegularCandy(CandyType type, int xIndex, int yIndex, Vector3 position)
    {
        if ((int)type >= _regularCandyPrefabs.Length || _regularCandyPrefabs[(int)type] == null)
        {
            Debug.LogError($"CandyFactory: No prefab for regular candy type {type}. Index out of bounds or null prefab.");
            return null;
        }
        GameObject prefabToUse = _regularCandyPrefabs[(int)type];
        return GetCandyFromPool(prefabToUse, type, xIndex, yIndex, position, false, SpecialCandyEffect.None);
    }

    public Candy CreateSpecialCandy(CandyType originalType, SpecialCandyEffect effect, int xIndex, int yIndex, Vector3 position)
    {
        GameObject prefabToUse = null;
        int typeIndex = (int)originalType;

        if (typeIndex >= _regularCandyPrefabs.Length) // Dựa trên số lượng kẹo cơ bản
        {
            Debug.LogError($"CandyFactory: Invalid originalType index {typeIndex} for special candy.");
            return null;
        }

        switch (effect)
        {
            case SpecialCandyEffect.ClearRow:
                if (typeIndex < _rowClearerPrefabs.Length && _rowClearerPrefabs[typeIndex] != null)
                    prefabToUse = _rowClearerPrefabs[typeIndex];
                else
                    Debug.LogError($"CandyFactory: No row clearer prefab for type {originalType} (index {typeIndex}).");
                break;
            case SpecialCandyEffect.ClearColumn:
                if (typeIndex < _columnClearerPrefabs.Length && _columnClearerPrefabs[typeIndex] != null)
                    prefabToUse = _columnClearerPrefabs[typeIndex];
                else
                    Debug.LogError($"CandyFactory: No column clearer prefab for type {originalType} (index {typeIndex}).");
                break;
            default:
                Debug.LogError($"CandyFactory: Attempted to create special candy with unsupported effect {effect}.");
                return null;
        }

        if (prefabToUse == null)
        {
            Debug.LogWarning($"CandyFactory: Prefab for special candy ({originalType}, {effect}) not found. Cannot create.");
            return null;
        }
        return GetCandyFromPool(prefabToUse, originalType, xIndex, yIndex, position, true, effect);
    }

    public void ReturnCandyToPool(Candy candy)
    {
        if (candy == null || candy.gameObject == null)
        {
            // Debug.LogWarning("CandyFactory: Attempted to return a null candy or candy with null GameObject.");
            return;
        }

        GameObject prefabOrigin = null; // Cần xác định prefab gốc của candy này
        int typeIndex = (int)candy.candyType;

        if (candy.isSpecial)
        {
            switch (candy.specialEffect)
            {
                case SpecialCandyEffect.ClearRow:
                    if (typeIndex < _rowClearerPrefabs.Length) prefabOrigin = _rowClearerPrefabs[typeIndex];
                    break;
                case SpecialCandyEffect.ClearColumn:
                    if (typeIndex < _columnClearerPrefabs.Length) prefabOrigin = _columnClearerPrefabs[typeIndex];
                    break;
            }
        }
        else
        {
            if (typeIndex < _regularCandyPrefabs.Length) prefabOrigin = _regularCandyPrefabs[typeIndex];
        }

        if (prefabOrigin == null)
        {
            Debug.LogWarning($"CandyFactory: Could not determine original prefab for candy {candy.name}. Destroying instead of pooling.");
            GameObject.Destroy(candy.gameObject);
            return;
        }

        string poolKey = GetPoolKey(prefabOrigin);
        if (_candyPools.ContainsKey(poolKey) && _candyPools[poolKey] != null)
        {
            candy.StopAllCoroutines(); // Quan trọng: Dừng tất cả coroutines
            // Các reset khác (isMoving, isMatched, sprite color) nên được xử lý trong Candy.Init()
            // hoặc một hàm ResetForPool() riêng trong Candy.
            _candyPools[poolKey].ReturnObject(candy.gameObject);
        }
        else
        {
            Debug.LogWarning($"CandyFactory: No pool found for prefab '{prefabOrigin.name}' (key: {poolKey}) when returning {candy.name}. Destroying instead.");
            GameObject.Destroy(candy.gameObject);
        }
    }

    public void ReturnAllCandiesToPools()
    {
        Debug.Log("CandyFactory: Returning all active candies to their respective pools.");
        foreach (var pool in _candyPools.Values)
        {
            if (pool != null)
            {
                pool.ReturnAllActiveObjects();
            }
        }
        // Log số lượng object trong pool sau khi return all
        // foreach(var kvp in _candyPools)
        // {
        //     if (kvp.Value != null)
        //         Debug.Log($"Pool {kvp.Key}: Active = {kvp.Value.GetActiveObjectCount()}, Pooled = {kvp.Value.GetPooledObjectCount()}");
        // }
    }
}
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
    private readonly GameObject _colorBombPrefab; // MODIFIED: Đổi thành GameObject duy nhất

    // MODIFIED: Cập nhật chữ ký của constructor
    public CandyFactory(GameObject[] regularCandyPrefabs, GameObject[] rowClearerPrefabs, GameObject[] columnClearerPrefabs, GameObject colorBombPrefab, Transform candyParentTransform, int initialPoolSize = 10)
    {
        _regularCandyPrefabs = regularCandyPrefabs;
        _rowClearerPrefabs = rowClearerPrefabs;
        _columnClearerPrefabs = columnClearerPrefabs;
        _colorBombPrefab = colorBombPrefab; // MODIFIED: Gán prefab mới
        _candyParentTransform = candyParentTransform;
        _initialPoolSizePerCandyType = initialPoolSize > 0 ? initialPoolSize : 1;

        // Validation cơ bản (giữ nguyên)
        if (_regularCandyPrefabs == null || _regularCandyPrefabs.Length == 0)
            Debug.LogError("CandyFactory Error: Regular candy prefabs array is null or empty.");
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

        // --- Initialize pools cho các kẹo theo màu ---
        for (int i = 0; i < _regularCandyPrefabs.Length; i++)
        {
            // Pool cho kẹo thường
            if (_regularCandyPrefabs[i] != null)
            {
                string key = GetPoolKey(_regularCandyPrefabs[i]);
                if (!_candyPools.ContainsKey(key))
                {
                    _candyPools[key] = new ObjectPool(_regularCandyPrefabs[i], _initialPoolSizePerCandyType, _candyParentTransform);
                }
            }

            // Pool cho kẹo xóa hàng
            if (i < _rowClearerPrefabs.Length && _rowClearerPrefabs[i] != null)
            {
                string key = GetPoolKey(_rowClearerPrefabs[i]);
                if (!_candyPools.ContainsKey(key))
                {
                    _candyPools[key] = new ObjectPool(_rowClearerPrefabs[i], _initialPoolSizePerCandyType / 2 > 0 ? _initialPoolSizePerCandyType / 2 : 1, _candyParentTransform);
                }
            }

            // Pool cho kẹo xóa cột
            if (i < _columnClearerPrefabs.Length && _columnClearerPrefabs[i] != null)
            {
                string key = GetPoolKey(_columnClearerPrefabs[i]);
                if (!_candyPools.ContainsKey(key))
                {
                    _candyPools[key] = new ObjectPool(_columnClearerPrefabs[i], _initialPoolSizePerCandyType / 2 > 0 ? _initialPoolSizePerCandyType / 2 : 1, _candyParentTransform);
                }
            }
        }

        // --- NEW: Initialize pool cho Color Bomb (riêng biệt) ---
        if (_colorBombPrefab != null)
        {
            string key = GetPoolKey(_colorBombPrefab);
            if (!_candyPools.ContainsKey(key))
            {
                // Pool size cho color bomb có thể nhỏ hơn một chút
                int colorBombPoolSize = _initialPoolSizePerCandyType / 3 > 0 ? _initialPoolSizePerCandyType / 3 : 1;
                _candyPools[key] = new ObjectPool(_colorBombPrefab, colorBombPoolSize, _candyParentTransform);
                Debug.Log($"Initialized pool for {key} with size {colorBombPoolSize}");
            }
        }
        else
        {
            Debug.LogError("CandyFactory: Color Bomb prefab is not assigned in the Inspector!");
        }

        Debug.Log($"CandyFactory: Object pools initialization complete. Total pools: {_candyPools.Count}");
    }

    public Candy CreateRegularCandy(CandyType type, int xIndex, int yIndex, Vector3 position)
    {
        if ((int)type >= _regularCandyPrefabs.Length || _regularCandyPrefabs[(int)type] == null)
        {
            Debug.LogError($"CandyFactory: No prefab for regular candy type {type}.");
            return null;
        }
        GameObject prefabToUse = _regularCandyPrefabs[(int)type];
        return GetCandyFromPool(prefabToUse, type, xIndex, yIndex, position, false, SpecialCandyEffect.None);
    }

    public Candy CreateSpecialCandy(CandyType originalType, SpecialCandyEffect effect, int xIndex, int yIndex, Vector3 position)
    {
        GameObject prefabToUse = null;
        int typeIndex = (int)originalType;

        if (typeIndex >= _regularCandyPrefabs.Length)
        {
            Debug.LogError($"CandyFactory: Invalid originalType index {typeIndex} for special candy.");
            return null;
        }

        // MODIFIED: Thêm case cho Color Bomb
        switch (effect)
        {
            case SpecialCandyEffect.ClearRow:
                if (typeIndex < _rowClearerPrefabs.Length) prefabToUse = _rowClearerPrefabs[typeIndex];
                break;
            case SpecialCandyEffect.ClearColumn:
                if (typeIndex < _columnClearerPrefabs.Length) prefabToUse = _columnClearerPrefabs[typeIndex];
                break;
            case SpecialCandyEffect.ClearColor: // NEW
                prefabToUse = _colorBombPrefab;
                break;
            default:
                Debug.LogError($"CandyFactory: Attempted to create special candy with unsupported effect {effect}.");
                return null;
        }

        if (prefabToUse == null)
        {
            Debug.LogWarning($"CandyFactory: Prefab for special candy ({originalType}, {effect}) not found or assigned. Cannot create.");
            return null;
        }
        return GetCandyFromPool(prefabToUse, originalType, xIndex, yIndex, position, true, effect);
    }

    public void ReturnCandyToPool(Candy candy)
    {
        if (candy == null || candy.gameObject == null) return;

        GameObject prefabOrigin = null;
        int typeIndex = (int)candy.candyType;

        if (candy.isSpecial)
        {
            // MODIFIED: Thêm case cho Color Bomb
            switch (candy.specialEffect)
            {
                case SpecialCandyEffect.ClearRow:
                    if (typeIndex < _rowClearerPrefabs.Length) prefabOrigin = _rowClearerPrefabs[typeIndex];
                    break;
                case SpecialCandyEffect.ClearColumn:
                    if (typeIndex < _columnClearerPrefabs.Length) prefabOrigin = _columnClearerPrefabs[typeIndex];
                    break;
                case SpecialCandyEffect.ClearColor: // NEW
                    prefabOrigin = _colorBombPrefab;
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
            _candyPools[poolKey].ReturnObject(candy.gameObject);
        }
        else
        {
            Debug.LogWarning($"CandyFactory: No pool found for prefab '{prefabOrigin.name}' (key: {poolKey}) when returning {candy.name}. Destroying instead.");
            GameObject.Destroy(candy.gameObject);
        }
    }

    // Các hàm GetCandyFromPool và ReturnAllCandiesToPools giữ nguyên, không cần thay đổi

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
            Debug.LogError($"CandyFactory: No pool for prefab '{prefab.name}' (key: {poolKey}). Ensure it's pre-initialized.");
            return null; // Tránh tạo pool on-the-fly để kiểm soát chặt chẽ hơn
        }

        GameObject candyGO = _candyPools[poolKey].GetObject();
        if (candyGO == null)
        {
            Debug.LogError($"CandyFactory: Pool for '{prefab.name}' returned null object.");
            return null;
        }

        candyGO.transform.position = position;
        candyGO.transform.rotation = Quaternion.identity;
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

    public void ReturnAllCandiesToPools()
    {
        Debug.Log("CandyFactory: Returning all active candies to their respective pools.");
        foreach (var pool in _candyPools.Values)
        {
            pool?.ReturnAllActiveObjects();
        }
    }
}
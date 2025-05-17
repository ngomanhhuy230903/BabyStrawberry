// CandyFactory.cs
using UnityEngine;
using System.Collections.Generic; // Cần cho List nếu GetAvailableCandyTypes được chuyển vào đây

public class CandyFactory
{
    private readonly GameObject[] _regularCandyPrefabs;
    private readonly GameObject[] _rowClearerPrefabs;
    private readonly GameObject[] _columnClearerPrefabs;
    private readonly Transform _candyParentTransform;

    public CandyFactory(GameObject[] regularCandyPrefabs, GameObject[] rowClearerPrefabs, GameObject[] columnClearerPrefabs, Transform candyParentTransform)
    {
        _regularCandyPrefabs = regularCandyPrefabs;
        _rowClearerPrefabs = rowClearerPrefabs;
        _columnClearerPrefabs = columnClearerPrefabs;
        _candyParentTransform = candyParentTransform;

        // Validation cơ bản
        if (_regularCandyPrefabs == null || _regularCandyPrefabs.Length == 0)
            Debug.LogError("CandyFactory Error: Regular candy prefabs array is null or empty.");
        if (_rowClearerPrefabs == null || _rowClearerPrefabs.Length != _regularCandyPrefabs.Length)
            Debug.LogError("CandyFactory Error: Row clearer prefabs array is null or doesn't match regular prefabs length.");
        if (_columnClearerPrefabs == null || _columnClearerPrefabs.Length != _regularCandyPrefabs.Length)
            Debug.LogError("CandyFactory Error: Column clearer prefabs array is null or doesn't match regular prefabs length.");
        if (_candyParentTransform == null)
            Debug.LogError("CandyFactory Error: Candy parent transform is null.");
    }

    public Candy CreateRegularCandy(CandyType type, int xIndex, int yIndex, Vector3 position)
    {
        if ((int)type >= _regularCandyPrefabs.Length || _regularCandyPrefabs[(int)type] == null)
        {
            Debug.LogError($"CandyFactory: No prefab for regular candy type {type}. Index out of bounds or null prefab.");
            return null;
        }

        GameObject candyGO = GameObject.Instantiate(_regularCandyPrefabs[(int)type], position, Quaternion.identity, _candyParentTransform);
        Candy candyComponent = candyGO.GetComponent<Candy>();

        if (candyComponent == null)
        {
            Debug.LogError($"CandyFactory: Prefab for {type} is missing Candy component. Destroying GameObject.");
            GameObject.Destroy(candyGO);
            return null;
        }

        candyComponent.Init(xIndex, yIndex, type, false, SpecialCandyEffect.None);
        // Tên GameObject sẽ được đặt trong Candy.Init()
        return candyComponent;
    }

    public Candy CreateSpecialCandy(CandyType originalType, SpecialCandyEffect effect, int xIndex, int yIndex, Vector3 position)
    {
        GameObject prefabToUse = null;
        int typeIndex = (int)originalType;

        if (typeIndex >= _regularCandyPrefabs.Length) // Kiểm tra chỉ số hợp lệ dựa trên số lượng loại kẹo cơ bản
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

        GameObject candyGO = GameObject.Instantiate(prefabToUse, position, Quaternion.identity, _candyParentTransform);
        Candy candyComponent = candyGO.GetComponent<Candy>();

        if (candyComponent == null)
        {
            Debug.LogError($"CandyFactory: Special prefab for {originalType}, {effect} is missing Candy component. Destroying GameObject.");
            GameObject.Destroy(candyGO);
            return null;
        }

        candyComponent.Init(xIndex, yIndex, originalType, true, effect);
        // Tên GameObject sẽ được đặt trong Candy.Init()
        return candyComponent;
    }
}
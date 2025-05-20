// ObjectPool.cs
using UnityEngine;
using System.Collections.Generic;

public class ObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform _parentTransform;
    private readonly Queue<GameObject> _pooledObjects = new Queue<GameObject>();
    private readonly List<GameObject> _activeObjects = new List<GameObject>(); // Theo dõi các đối tượng đang hoạt động

    public int InitialPoolSize { get; private set; }
    public string PrefabName => _prefab != null ? _prefab.name : "NULL_PREFAB";

    public ObjectPool(GameObject prefab, int initialSize, Transform parentTransform = null)
    {
        if (prefab == null)
        {
            Debug.LogError("ObjectPool Constructor: Prefab cannot be null.");
            // Không thể tiếp tục nếu prefab null, hoặc phải có cơ chế xử lý lỗi khác
            return;
        }
        _prefab = prefab;
        _parentTransform = parentTransform;
        InitialPoolSize = initialSize;

        for (int i = 0; i < initialSize; i++)
        {
            AddObjectToPool(false); // Thêm vào và giữ inactive
        }
    }

    private GameObject CreateNewObjectForPool()
    {
        if (_prefab == null)
        {
            Debug.LogError($"ObjectPool ({PrefabName}): Prefab is null. Cannot create new object.");
            return null;
        }
        GameObject newObject = GameObject.Instantiate(_prefab, _parentTransform);
        // Đảm bảo component Candy tồn tại trên prefab, nếu không thì pool này không hợp lệ cho Candy
        if (newObject.GetComponent<Candy>() == null)
        {
            Debug.LogError($"ObjectPool ({PrefabName}): Prefab '{_prefab.name}' is missing the Candy component. This pool might not function correctly for Candies.");
        }
        return newObject;
    }

    private void AddObjectToPool(bool isActive)
    {
        GameObject newObject = CreateNewObjectForPool();
        if (newObject != null)
        {
            newObject.SetActive(isActive);
            if (!isActive)
            {
                _pooledObjects.Enqueue(newObject);
            }
            else
            {
                _activeObjects.Add(newObject);
            }
        }
    }

    public GameObject GetObject()
    {
        GameObject pooledObject;
        if (_pooledObjects.Count > 0)
        {
            pooledObject = _pooledObjects.Dequeue();
            // Debug.Log($"ObjectPool ({PrefabName}): Reusing object from pool. Pool size now: {_pooledObjects.Count}");
        }
        else
        {
            Debug.LogWarning($"ObjectPool ({PrefabName}): Pool empty, instantiating new object. Consider increasing initial pool size if this happens frequently.");
            pooledObject = CreateNewObjectForPool();
            if (pooledObject == null) return null; // Không thể tạo object mới
        }

        pooledObject.SetActive(true);
        _activeObjects.Add(pooledObject);
        return pooledObject;
    }

    public void ReturnObject(GameObject objectToReturn)
    {
        if (objectToReturn == null)
        {
            Debug.LogWarning($"ObjectPool ({PrefabName}): Tried to return a null object.");
            return;
        }

        // Kiểm tra xem object có thực sự thuộc pool này không (dựa trên prefab name hoặc tag)
        // Điều này có thể phức tạp, tạm thời bỏ qua để đơn giản.
        // Quan trọng là objectToReturn phải là instance của _prefab.

        objectToReturn.SetActive(false);
        if (_parentTransform != null) // Đảm bảo nó quay về đúng parent của pool
        {
            objectToReturn.transform.SetParent(_parentTransform);
        }

        bool removed = _activeObjects.Remove(objectToReturn);
        if (!removed)
        {
            // Debug.LogWarning($"ObjectPool ({PrefabName}): Returned object '{objectToReturn.name}' was not in the active list. It might have been returned twice or belongs to another pool.");
        }

        // Chỉ enqueue nếu nó chưa có trong pool (tránh duplicate)
        if (!_pooledObjects.Contains(objectToReturn))
        {
            _pooledObjects.Enqueue(objectToReturn);
            // Debug.Log($"ObjectPool ({PrefabName}): Object '{objectToReturn.name}' returned. Pool size now: {_pooledObjects.Count}");
        }
        else
        {
            // Debug.LogWarning($"ObjectPool ({PrefabName}): Object '{objectToReturn.name}' is already in the pool. Not enqueuing again.");
            // Nếu đã có trong pool mà vẫn được return, có thể do lỗi logic ở đâu đó.
            // Có thể hủy object này để tránh lỗi.
            GameObject.Destroy(objectToReturn);
        }
    }

    public void ReturnAllActiveObjects()
    {
        // Tạo bản sao để tránh lỗi khi sửa đổi collection đang duyệt
        List<GameObject> activeObjectsCopy = new List<GameObject>(_activeObjects);
        foreach (GameObject activeObject in activeObjectsCopy)
        {
            ReturnObject(activeObject); // ReturnObject sẽ xử lý việc xóa khỏi _activeObjects
        }
        // _activeObjects.Clear(); // ReturnObject đã làm điều này
    }

    public int GetActiveObjectCount() => _activeObjects.Count;
    public int GetPooledObjectCount() => _pooledObjects.Count;
}
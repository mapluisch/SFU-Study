using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    public static UnityMainThreadDispatcher Instance { get; private set; }

    private readonly Queue<Action> _executionQueue = new Queue<Action>();
    private readonly object _lockObject = new object();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnRuntimeMethodLoad()
    {
        if (Instance == null)
        {
            var go = new GameObject("UnityMainThreadDispatcher");
            Instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (_lockObject)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock (_lockObject)
        {
            _executionQueue.Enqueue(action);
        }
    }

    public async Task EnqueueAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();

        Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        await tcs.Task;
    }

    public async Task<T> EnqueueAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();

        Enqueue(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return await tcs.Task;
    }
}
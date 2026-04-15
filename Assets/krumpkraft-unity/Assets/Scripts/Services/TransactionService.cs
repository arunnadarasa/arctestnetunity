using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using System.Reflection;
using UnityEngine;

namespace KrumpKraft
{
    public class TransactionService : MonoBehaviour
    {
        private WalletService _wallet;
        private object _web3;
        private object _account;
        private string _rpcUrl;
        private bool _initialized;

        private void Start()
        {
            var manager = KrumpKraftManager.Instance;
            if (manager != null)
            {
                _wallet = manager.WalletService;
                _rpcUrl = manager.ChainSettings.HostRpcUrl;
                
                if (_wallet != null)
                {
                    _wallet.OnConnected += OnWalletConnected;
                    _wallet.OnDisconnected += OnWalletDisconnected;
                    Debug.Log("[TransactionService] Subscribed to wallet connect/disconnect events");
                }
            }
        }

        private void OnDestroy()
        {
            if (_wallet != null)
            {
                _wallet.OnConnected -= OnWalletConnected;
                _wallet.OnDisconnected -= OnWalletDisconnected;
            }
        }

        private void OnWalletConnected(string address)
        {
            Debug.Log($"[TransactionService] Wallet connected: {address}. Resetting Web3 cache to use new provider key.");
            ResetWeb3Cache();
        }

        private void OnWalletDisconnected()
        {
            Debug.Log("[TransactionService] Wallet disconnected. Resetting Web3 cache.");
            ResetWeb3Cache();
        }

        private void ResetWeb3Cache()
        {
            _initialized = false;
            _account = null;
            _web3 = null;
            Debug.Log("[TransactionService] Web3 cache cleared. Next operation will reinitialize with current provider.");
        }

        private void InitializeWeb3()
        {
            if (_initialized && _web3 != null) return;
            
            if (_wallet == null || !_wallet.IsConnected)
            {
                Debug.LogError("[TransactionService] Cannot initialize Web3: Wallet not connected");
                return;
            }

            var privateKey = _wallet.Provider?.GetPrivateKey();
            if (string.IsNullOrEmpty(privateKey))
            {
                Debug.LogError("[TransactionService] Cannot initialize Web3: Private key not available");
                return;
            }
            
            Debug.Log($"[TransactionService] Got private key from wallet (first 10 chars): {privateKey.Substring(0, Math.Min(10, privateKey.Length))}...");
            Debug.Log($"[TransactionService] Wallet connected address: {_wallet.ConnectedAddress}");

            if (string.IsNullOrEmpty(_rpcUrl))
            {
                Debug.LogError("[TransactionService] Cannot initialize Web3: RPC URL not set");
                return;
            }

            try
            {
                Debug.Log($"[TransactionService] Initializing Web3 with RPC: {_rpcUrl}");
                
                // Remove 0x prefix if present
                var key = privateKey;
                if (key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    key = key.Substring(2);
                
                // Try to create Account with BigInteger chain ID
                var accountType = Type.GetType("Nethereum.Web3.Accounts.Account, Nethereum.Accounts");
                if (accountType == null)
                {
                    accountType = Type.GetType("Nethereum.Web3.Accounts.Account, Nethereum.Web3");
                }
                
                if (accountType == null)
                {
                    Debug.LogError("[TransactionService] Could not find Account type");
                    return;
                }

                var web3Type = Type.GetType("Nethereum.Web3.Web3, Nethereum.Web3");
                if (web3Type == null)
                {
                    Debug.LogError("[TransactionService] Nethereum.Web3.Web3 type not found.");
                    return;
                }

                Debug.Log($"[TransactionService] Creating Account...");
                
                object account = null;
                
                // Try with BigInteger chain ID
                try
                {
                    var chainId = new BigInteger(5042002); // Arc Testnet
                    account = Activator.CreateInstance(accountType, new object[] { key, chainId });
                    Debug.Log($"[TransactionService] Created Account with BigInteger chainId");
                    
                    // Log the account address
                    var addressProperty = accountType.GetProperty("Address");
                    if (addressProperty != null)
                    {
                        var accountAddress = addressProperty.GetValue(account)?.ToString();
                        Debug.Log($"[TransactionService] Account address from private key: {accountAddress}");
                        Debug.Log($"[TransactionService] Wallet connected address: {_wallet.ConnectedAddress}");
                        
                        if (!string.Equals(accountAddress, _wallet.ConnectedAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.LogError($"[TransactionService] ADDRESS MISMATCH! Account: {accountAddress}, Wallet: {_wallet.ConnectedAddress}");
                        }
                    }
                }
                catch (Exception e1)
                {
                    Debug.LogWarning($"[TransactionService] Failed with BigInteger chainId: {e1.Message}");
                    
                    // Try with just string private key
                    try
                    {
                        account = Activator.CreateInstance(accountType, new object[] { key });
                        Debug.Log("[TransactionService] Created Account with private key string");
                    }
                    catch (Exception e2)
                    {
                        Debug.LogWarning($"[TransactionService] Failed with string key: {e2.Message}");
                        
                        // Try using static Account.LoadPrivate method if it exists
                        try
                        {
                            var loadMethod = accountType.GetMethod("LoadPrivate", BindingFlags.Public | BindingFlags.Static);
                            if (loadMethod != null)
                            {
                                account = loadMethod.Invoke(null, new object[] { key });
                                Debug.Log("[TransactionService] Created Account with LoadPrivate");
                            }
                        }
                        catch (Exception e3)
                        {
                            Debug.LogWarning($"[TransactionService] Failed with LoadPrivate: {e3.Message}");
                        }
                    }
                }
                
                if (account == null)
                {
                    Debug.LogError("[TransactionService] Failed to create Account with any method. Listing constructors:");
                    foreach (var ctor in accountType.GetConstructors())
                    {
                        var parameters = ctor.GetParameters();
                        var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Debug.Log($"  Constructor({paramStr})");
                    }
                    return;
                }
                
                _account = account;
                Debug.Log("[TransactionService] Account created successfully");

                // Create RPC Client first
                Debug.Log("[TransactionService] Creating RPC client...");
                var rpcClientType = Type.GetType("Nethereum.JsonRpc.Client.RpcClient, Nethereum.JsonRpc.RpcClient");
                if (rpcClientType == null)
                {
                    Debug.LogError("[TransactionService] RpcClient type not found");
                    return;
                }

                object rpcClient = null;
                try
                {
                    // Just use the first constructor with 5 nulls - Activator will resolve correctly
                    var constructors = rpcClientType.GetConstructors();
                    if (constructors.Length > 0)
                    {
                        var firstCtor = constructors[0];
                        var paramCount = firstCtor.GetParameters().Length;
                        var args = new object[paramCount];
                        args[0] = new Uri(_rpcUrl); // First param is Uri
                        // Rest are null
                        
                        rpcClient = firstCtor.Invoke(args);
                        Debug.Log($"[TransactionService] Created RPC client for {_rpcUrl}");
                    }
                    else
                    {
                        Debug.LogError("[TransactionService] No constructors found for RpcClient");
                        return;
                    }
                }
                catch (Exception eRpc)
                {
                    Debug.LogError($"[TransactionService] Failed to create RPC client: {eRpc.Message}\n{eRpc.StackTrace}");
                    return;
                }

                // Create Web3 instance with Account and IClient
                Debug.Log("[TransactionService] Creating Web3 with Account and IClient...");
                try
                {
                    _web3 = Activator.CreateInstance(web3Type, new object[] { _account, rpcClient });
                    Debug.Log("[TransactionService] Web3 initialized successfully");
                    _initialized = true;
                }
                catch (Exception eWeb3)
                {
                    Debug.LogError($"[TransactionService] Failed to create Web3 with (Account, IClient): {eWeb3.Message}");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TransactionService] Failed to initialize Web3: {e.Message}\n{e.StackTrace}");
            }
        }

        public void SendJABToCore(string toAddress, string token, string amountWei, string priorityFeeWei, 
            string senderExecutor, string nonce, bool isAsyncExec, string signature, Action<bool, string> onComplete)
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                onComplete?.Invoke(false, "Wallet not connected");
                return;
            }

            InitializeWeb3();
            if (_web3 == null)
            {
                onComplete?.Invoke(false, "Web3 not initialized. Check console for errors.");
                return;
            }

            Debug.Log($"[TransactionService] Sending JAB to Core: to={toAddress}, amount={amountWei}");
            
            var coreAddress = ContractAddresses.Core;
            var fromAddress = _wallet.ConnectedAddress;
            
            Debug.Log($"[TransactionService] Using 'from' address: {fromAddress}");
            Debug.Log($"[TransactionService] This should match the address that signed the message");

            StartCoroutine(SendCorePayTransaction(fromAddress, toAddress, token, amountWei, priorityFeeWei, 
                senderExecutor, nonce, isAsyncExec, signature, coreAddress, onComplete));
        }
        
        public void GetNextSyncNonce(string fromAddress, Action<bool, string> onComplete)
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                onComplete?.Invoke(false, "Wallet not connected");
                return;
            }

            InitializeWeb3();
            if (_web3 == null)
            {
                onComplete?.Invoke(false, "Web3 not initialized");
                return;
            }

            StartCoroutine(FetchNextSyncNonce(fromAddress, onComplete));
        }
        
        private IEnumerator FetchNextSyncNonce(string fromAddress, Action<bool, string> onComplete)
        {
            var taskCompleted = false;
            string nonce = null;
            Exception error = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var coreAddress = ContractAddresses.Core;
                    var coreAbi = @"[{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""}],""name"":""getNextCurrentSyncNonce"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}]";
                    
                    var web3Type = _web3.GetType();
                    var ethProperty = web3Type.GetProperty("Eth");
                    var eth = ethProperty.GetValue(_web3);
                    
                    var getContractMethod = eth.GetType().GetMethod("GetContract", new[] { typeof(string), typeof(string) });
                    var contract = getContractMethod.Invoke(eth, new object[] { coreAbi, coreAddress });
                    
                    var getFunctionMethod = contract.GetType().GetMethod("GetFunction", new[] { typeof(string) });
                    var nonceFunction = getFunctionMethod.Invoke(contract, new object[] { "getNextCurrentSyncNonce" });
                    
                    var callMethodGeneric = nonceFunction.GetType().GetMethod("CallAsync", 
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(object[]) },
                        null);
                    
                    if (callMethodGeneric == null)
                    {
                        throw new Exception("CallAsync method not found");
                    }
                    
                    var callMethod = callMethodGeneric.MakeGenericMethod(typeof(BigInteger));
                    var task = callMethod.Invoke(nonceFunction, new object[] { new object[] { fromAddress } });
                    
                    var taskType = task.GetType();
                    var isCompletedProperty = taskType.GetProperty("IsCompleted");
                    var isFaultedProperty = taskType.GetProperty("IsFaulted");
                    var exceptionProperty = taskType.GetProperty("Exception");
                    var resultProperty = taskType.GetProperty("Result");
                    
                    while (!(bool)isCompletedProperty.GetValue(task))
                        System.Threading.Thread.Sleep(100);
                    
                    if ((bool)isFaultedProperty.GetValue(task))
                    {
                        error = exceptionProperty.GetValue(task) as Exception;
                    }
                    else
                    {
                        var result = resultProperty.GetValue(task);
                        nonce = result.ToString();
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    taskCompleted = true;
                }
            });
            
            thread.Start();
            
            while (!taskCompleted)
                yield return null;
            
            if (error != null)
            {
                Debug.LogError($"[TransactionService] Failed to fetch sync nonce: {error.Message}");
                onComplete?.Invoke(false, error.Message);
            }
            else
            {
                Debug.Log($"[TransactionService] Fetched sync nonce: {nonce}");
                onComplete?.Invoke(true, nonce);
            }
        }

        private IEnumerator SendCorePayTransaction(string fromAddress, string toAddress, string token, 
            string amountWei, string priorityFeeWei, string senderExecutor, string nonce, bool isAsyncExec, 
            string signature, string coreAddress, Action<bool, string> onComplete)
        {
            var taskCompleted = false;
            var txHash = "";
            Exception error = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    Debug.Log($"[TransactionService] Calling Core.pay() at {coreAddress}");
                    
                    var coreAbi = @"[{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""toAddress"",""type"":""address""},{""internalType"":""string"",""name"":""toIdentity"",""type"":""string""},{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""priorityFee"",""type"":""uint256""},{""internalType"":""address"",""name"":""executor"",""type"":""address""},{""internalType"":""uint256"",""name"":""nonce"",""type"":""uint256""},{""internalType"":""bool"",""name"":""isAsyncExec"",""type"":""bool""},{""internalType"":""bytes"",""name"":""signature"",""type"":""bytes""}],""name"":""pay"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}]";
                    
                    var accountType = _account.GetType();
                    var transactionManagerProperty = accountType.GetProperty("TransactionManager");
                    var transactionManager = transactionManagerProperty.GetValue(_account);
                    
                    var estimateGasProperty = transactionManager.GetType().GetProperty("EstimateGas");
                    if (estimateGasProperty != null)
                    {
                        estimateGasProperty.SetValue(transactionManager, false);
                        Debug.Log("[TransactionService] Disabled automatic gas estimation");
                    }
                    
                    var defaultGasProperty = transactionManager.GetType().GetProperty("DefaultGas");
                    if (defaultGasProperty != null)
                    {
                        defaultGasProperty.SetValue(transactionManager, new BigInteger(200000));
                        Debug.Log("[TransactionService] Set default gas to 200000");
                    }
                    
                    var web3Type = _web3.GetType();
                    var ethProperty = web3Type.GetProperty("Eth");
                    var eth = ethProperty.GetValue(_web3);
                    
                    var getContractMethod = eth.GetType().GetMethod("GetContract", new[] { typeof(string), typeof(string) });
                    var contract = getContractMethod.Invoke(eth, new object[] { coreAbi, coreAddress });
                    
                    var getFunctionMethod = contract.GetType().GetMethod("GetFunction", new[] { typeof(string) });
                    var payFunction = getFunctionMethod.Invoke(contract, new object[] { "pay" });
                    
                    var signatureBytes = HexStringToByteArray(signature);
                    var amountBigInt = BigInteger.Parse(amountWei);
                    var priorityFeeBigInt = BigInteger.Parse(priorityFeeWei);
                    var nonceBigInt = BigInteger.Parse(nonce);
                    
                    var parameters = new object[] 
                    { 
                        fromAddress, toAddress, "", token, amountBigInt, priorityFeeBigInt, 
                        senderExecutor, nonceBigInt, isAsyncExec, signatureBytes 
                    };
                    
                    var sendTransactionMethod = payFunction.GetType().GetMethod("SendTransactionAsync", 
                        new[] { typeof(string), typeof(object[]) });
                    
                    if (sendTransactionMethod == null)
                    {
                        throw new Exception("SendTransactionAsync method not found on pay function");
                    }
                    
                    var task = sendTransactionMethod.Invoke(payFunction, new object[] { fromAddress, parameters });
                    
                    var taskType = task.GetType();
                    var isCompletedProperty = taskType.GetProperty("IsCompleted");
                    var isFaultedProperty = taskType.GetProperty("IsFaulted");
                    var exceptionProperty = taskType.GetProperty("Exception");
                    var resultProperty = taskType.GetProperty("Result");
                    
                    while (!(bool)isCompletedProperty.GetValue(task))
                        System.Threading.Thread.Sleep(100);
                    
                    if ((bool)isFaultedProperty.GetValue(task))
                    {
                        error = exceptionProperty.GetValue(task) as Exception;
                    }
                    else
                    {
                        txHash = resultProperty.GetValue(task) as string;
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    taskCompleted = true;
                }
            });
            
            thread.Start();
            
            while (!taskCompleted)
                yield return null;
            
            if (error != null)
            {
                Debug.LogError($"[TransactionService] Core.pay() failed: {error.Message}\n{error.StackTrace}");
                onComplete?.Invoke(false, error.Message);
            }
            else
            {
                Debug.Log($"[TransactionService] Core.pay() successful! TX: {txHash}");
                onComplete?.Invoke(true, txHash);
            }
        }

        public void SendERC20Transfer(string tokenAddress, string toAddress, string amountWei, Action<bool, string> onComplete)
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                onComplete?.Invoke(false, "Wallet not connected");
                return;
            }

            InitializeWeb3();
            if (_web3 == null)
            {
                onComplete?.Invoke(false, "Web3 not initialized. Check console for errors.");
                return;
            }

            Debug.Log($"[TransactionService] Sending ERC20 transfer: token={tokenAddress}, to={toAddress}, amount={amountWei}");
            
            var fromAddress = _wallet.ConnectedAddress;
            StartCoroutine(SendERC20TransferTransaction(fromAddress, tokenAddress, toAddress, amountWei, onComplete));
        }

        /// <summary>Approve ERC20 token for a spender (e.g. USDC.k for EVVM Treasury). Required before Deposit to EVVM.</summary>
        public void ApproveErc20(string tokenAddress, string spenderAddress, string amountWei, Action<bool, string> onComplete)
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                onComplete?.Invoke(false, "Wallet not connected");
                return;
            }
            InitializeWeb3();
            if (_web3 == null)
            {
                onComplete?.Invoke(false, "Web3 not initialized.");
                return;
            }
            var fromAddress = _wallet.ConnectedAddress;
            StartCoroutine(SendErc20ApproveTransaction(fromAddress, tokenAddress, spenderAddress, amountWei, onComplete));
        }

        /// <summary>Deposit USDC.k into EVVM Treasury so "Pay via x402" can debit internal balance. Call after Approve.</summary>
        public void DepositToEvvmTreasury(string amountWei, Action<bool, string> onComplete)
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                onComplete?.Invoke(false, "Wallet not connected");
                return;
            }
            InitializeWeb3();
            if (_web3 == null)
            {
                onComplete?.Invoke(false, "Web3 not initialized.");
                return;
            }
            var fromAddress = _wallet.ConnectedAddress;
            StartCoroutine(SendEvvmTreasuryDepositTransaction(fromAddress, amountWei, onComplete));
        }

        private IEnumerator SendErc20ApproveTransaction(string fromAddress, string tokenAddress, string spenderAddress,
            string amountWei, Action<bool, string> onComplete)
        {
            var taskCompleted = false;
            var txHash = "";
            Exception error = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    SetDefaultGas(100000);
                    var approveAbi = @"[{""constant"":false,""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""}]";
                    var web3Type = _web3.GetType();
                    var eth = web3Type.GetProperty("Eth").GetValue(_web3);
                    var getContractMethod = eth.GetType().GetMethod("GetContract", new[] { typeof(string), typeof(string) });
                    var contract = getContractMethod.Invoke(eth, new object[] { approveAbi, tokenAddress });
                    var getFunctionMethod = contract.GetType().GetMethod("GetFunction", new[] { typeof(string) });
                    var approveFunction = getFunctionMethod.Invoke(contract, new object[] { "approve" });
                    var amountBigInt = BigInteger.Parse(amountWei);
                    var sendMethod = approveFunction.GetType().GetMethod("SendTransactionAsync", new[] { typeof(string), typeof(object[]) });
                    var task = sendMethod.Invoke(approveFunction, new object[] { fromAddress, new object[] { spenderAddress, amountBigInt } });
                    WaitTaskResult(task, out txHash, out error);
                }
                catch (Exception e) { error = e; }
                finally { taskCompleted = true; }
            });
            thread.Start();
            while (!taskCompleted) yield return null;
            onComplete?.Invoke(error == null, error != null ? error.Message : txHash);
        }

        private IEnumerator SendEvvmTreasuryDepositTransaction(string fromAddress, string amountWei, Action<bool, string> onComplete)
        {
            var taskCompleted = false;
            var txHash = "";
            Exception error = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    SetDefaultGas(150000);
                    var treasuryAbi = @"[{""inputs"":[{""internalType"":""address"",""name"":""token"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""deposit"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}]";
                    var web3Type = _web3.GetType();
                    var eth = web3Type.GetProperty("Eth").GetValue(_web3);
                    var getContractMethod = eth.GetType().GetMethod("GetContract", new[] { typeof(string), typeof(string) });
                    var contract = getContractMethod.Invoke(eth, new object[] { treasuryAbi, ContractAddresses.Treasury });
                    var getFunctionMethod = contract.GetType().GetMethod("GetFunction", new[] { typeof(string) });
                    var depositFunction = getFunctionMethod.Invoke(contract, new object[] { "deposit" });
                    var amountBigInt = BigInteger.Parse(amountWei);
                    var sendMethod = depositFunction.GetType().GetMethod("SendTransactionAsync", new[] { typeof(string), typeof(object[]) });
                    var task = sendMethod.Invoke(depositFunction, new object[] { fromAddress, new object[] { ContractAddresses.USDCHealth, amountBigInt } });
                    WaitTaskResult(task, out txHash, out error);
                }
                catch (Exception e) { error = e; }
                finally { taskCompleted = true; }
            });
            thread.Start();
            while (!taskCompleted) yield return null;
            onComplete?.Invoke(error == null, error != null ? error.Message : txHash);
        }

        private static void WaitTaskResult(object task, out string txHash, out Exception error)
        {
            txHash = "";
            error = null;
            var taskType = task.GetType();
            var isCompleted = taskType.GetProperty("IsCompleted");
            var isFaulted = taskType.GetProperty("IsFaulted");
            var exceptionProp = taskType.GetProperty("Exception");
            var resultProp = taskType.GetProperty("Result");
            while (!(bool)isCompleted.GetValue(task))
                System.Threading.Thread.Sleep(100);
            if ((bool)isFaulted.GetValue(task))
                error = exceptionProp.GetValue(task) as Exception;
            else
                txHash = resultProp.GetValue(task) as string;
        }

        private void SetDefaultGas(BigInteger gas)
        {
            if (_account == null) return;
            var accountType = _account.GetType();
            var txManagerProperty = accountType.GetProperty("TransactionManager");
            var txManager = txManagerProperty?.GetValue(_account);
            if (txManager != null)
                txManager.GetType().GetProperty("DefaultGas")?.SetValue(txManager, gas);
        }

        private IEnumerator SendERC20TransferTransaction(string fromAddress, string tokenAddress, string toAddress, 
            string amountWei, Action<bool, string> onComplete)
        {
            var taskCompleted = false;
            var txHash = "";
            Exception error = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    Debug.Log($"[TransactionService] Calling ERC20.transfer() on {tokenAddress}");
                    
                    // Set gas limit so ERC20 transfer doesn't use default 21000 (too low; need ~22k+)
                    var accountType = _account.GetType();
                    var txManagerProperty = accountType.GetProperty("TransactionManager");
                    var txManager = txManagerProperty?.GetValue(_account);
                    if (txManager != null)
                    {
                        var defaultGasProp = txManager.GetType().GetProperty("DefaultGas");
                        defaultGasProp?.SetValue(txManager, new BigInteger(100000));
                    }
                    
                    var erc20Abi = @"[{""constant"":false,""inputs"":[{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}]";
                    
                    var web3Type = _web3.GetType();
                    var ethProperty = web3Type.GetProperty("Eth");
                    var eth = ethProperty.GetValue(_web3);
                    
                    var getContractMethod = eth.GetType().GetMethod("GetContract", new[] { typeof(string), typeof(string) });
                    var contract = getContractMethod.Invoke(eth, new object[] { erc20Abi, tokenAddress });
                    
                    var getFunctionMethod = contract.GetType().GetMethod("GetFunction", new[] { typeof(string) });
                    var transferFunction = getFunctionMethod.Invoke(contract, new object[] { "transfer" });
                    
                    var amountBigInt = BigInteger.Parse(amountWei);
                    
                    var sendTransactionMethod = transferFunction.GetType().GetMethod("SendTransactionAsync", 
                        new[] { typeof(string), typeof(object[]) });
                    
                    var parameters = new object[] { toAddress, amountBigInt };
                    
                    var task = sendTransactionMethod.Invoke(transferFunction, new object[] { fromAddress, parameters });
                    
                    var taskType = task.GetType();
                    var isCompletedProperty = taskType.GetProperty("IsCompleted");
                    var isFaultedProperty = taskType.GetProperty("IsFaulted");
                    var exceptionProperty = taskType.GetProperty("Exception");
                    var resultProperty = taskType.GetProperty("Result");
                    
                    while (!(bool)isCompletedProperty.GetValue(task))
                        System.Threading.Thread.Sleep(100);
                    
                    if ((bool)isFaultedProperty.GetValue(task))
                    {
                        error = exceptionProperty.GetValue(task) as Exception;
                    }
                    else
                    {
                        txHash = resultProperty.GetValue(task) as string;
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    taskCompleted = true;
                }
            });
            
            thread.Start();
            
            while (!taskCompleted)
                yield return null;
            
            if (error != null)
            {
                Debug.LogError($"[TransactionService] ERC20.transfer() failed: {error.Message}\n{error.StackTrace}");
                onComplete?.Invoke(false, error.Message);
            }
            else
            {
                Debug.Log($"[TransactionService] ERC20.transfer() successful! TX: {txHash}");
                onComplete?.Invoke(true, txHash);
            }
        }

        public void SendNativeTransaction(string toAddress, string amountWei, Action<bool, string> onComplete)
        {
            if (_wallet == null || !_wallet.IsConnected)
            {
                onComplete?.Invoke(false, "Wallet not connected");
                return;
            }

            InitializeWeb3();
            if (_web3 == null)
            {
                onComplete?.Invoke(false, "Web3 not initialized. Check console for errors.");
                return;
            }

            Debug.Log($"[TransactionService] Sending native transaction: to={toAddress}, amount={amountWei}");
            
            var fromAddress = _wallet.ConnectedAddress;
            StartCoroutine(SendNativeTransactionCoroutine(fromAddress, toAddress, amountWei, onComplete));
        }

        private IEnumerator SendNativeTransactionCoroutine(string fromAddress, string toAddress, string amountWei, 
            Action<bool, string> onComplete)
        {
            var taskCompleted = false;
            var txHash = "";
            Exception error = null;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    Debug.Log($"[TransactionService] Sending native transfer from {fromAddress} to {toAddress}");
                    
                    var web3Type = _web3.GetType();
                    var ethProperty = web3Type.GetProperty("Eth");
                    var eth = ethProperty.GetValue(_web3);
                    
                    var transactionManagerProperty = eth.GetType().GetProperty("TransactionManager");
                    var transactionManager = transactionManagerProperty.GetValue(eth);
                    
                    var amountBigInt = BigInteger.Parse(amountWei);
                    var hexBigIntegerType = Type.GetType("Nethereum.Hex.HexTypes.HexBigInteger, Nethereum.Hex");
                    var hexAmount = Activator.CreateInstance(hexBigIntegerType, new object[] { amountBigInt });
                    
                    var sendTransactionMethod = transactionManager.GetType().GetMethod("SendTransactionAsync", 
                        new[] { typeof(string), typeof(string), hexBigIntegerType });
                    
                    var task = sendTransactionMethod.Invoke(transactionManager, 
                        new object[] { fromAddress, toAddress, hexAmount });
                    
                    var taskType = task.GetType();
                    var isCompletedProperty = taskType.GetProperty("IsCompleted");
                    var isFaultedProperty = taskType.GetProperty("IsFaulted");
                    var exceptionProperty = taskType.GetProperty("Exception");
                    var resultProperty = taskType.GetProperty("Result");
                    
                    while (!(bool)isCompletedProperty.GetValue(task))
                        System.Threading.Thread.Sleep(100);
                    
                    if ((bool)isFaultedProperty.GetValue(task))
                    {
                        error = exceptionProperty.GetValue(task) as Exception;
                    }
                    else
                    {
                        txHash = resultProperty.GetValue(task) as string;
                    }
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    taskCompleted = true;
                }
            });
            
            thread.Start();
            
            while (!taskCompleted)
                yield return null;
            
            if (error != null)
            {
                Debug.LogError($"[TransactionService] Native transaction failed: {error.Message}\n{error.StackTrace}");
                onComplete?.Invoke(false, error.Message);
            }
            else
            {
                Debug.Log($"[TransactionService] Native transaction successful! TX: {txHash}");
                onComplete?.Invoke(true, txHash);
            }
        }

        private byte[] HexStringToByteArray(string hex)
        {
            if (hex.StartsWith("0x") || hex.StartsWith("0X"))
                hex = hex.Substring(2);
            
            var numberChars = hex.Length;
            var bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}

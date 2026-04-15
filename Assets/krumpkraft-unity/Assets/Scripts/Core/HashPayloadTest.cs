using System;
using System.Numerics;
using UnityEngine;

namespace KrumpKraft
{
    public class HashPayloadTest : MonoBehaviour
    {
        [ContextMenu("Test Hash Payload")]
        public void TestHashPayload()
        {
            var receiver = "0xc9789298e98131a167fd1011cefe7909126af870";
            var token = "0x0000000000000000000000000000000000000001";
            var amount = "1000000000000000000";
            var priorityFee = "0";
            
            Debug.Log("=== Testing Hash Payload ===");
            Debug.Log($"Receiver: {receiver}");
            Debug.Log($"Token: {token}");
            Debug.Log($"Amount: {amount}");
            Debug.Log($"PriorityFee: {priorityFee}");
            
            var hashPayload = PayPayloadBuilder.HashDataForPay(receiver, "", token, amount, priorityFee);
            
            Debug.Log($"Hash Payload: {hashPayload}");
            
            var evvmId = PayPayloadBuilder.EvvmId;
            var coreAddress = ContractAddresses.Core;
            var executor = ContractAddresses.ZeroAddress;
            var nonce = "0";
            var isAsyncExec = false;
            
            var message = $"{evvmId},{coreAddress},{hashPayload},{executor},{nonce},{isAsyncExec.ToString().ToLower()}";
            Debug.Log($"Full message: {message}");
            
            Debug.Log("Expected Solidity encoding:");
            Debug.Log("  receiver as bytes32: 000000000000000000000000c9789298e98131a167fd1011cefe7909126af870");
            Debug.Log("  token as address:    0000000000000000000000000000000000000001");
            Debug.Log("  amount as uint256:   0000000000000000000000000000000000000000000000000de0b6b3a7640000");
            Debug.Log("  priorityFee uint256: 0000000000000000000000000000000000000000000000000000000000000000");
            Debug.Log("  Total packed: 116 bytes");
        }
        
        [ContextMenu("Test Message With Nonce 10")]
        public void TestWithNonce10()
        {
            var receiver = "0xc9789298e98131a167fd1011cefe7909126af870";
            var token = "0x0000000000000000000000000000000000000001";
            var amount = "1000000000000000000";
            var priorityFee = "0";
            
            var hashPayload = PayPayloadBuilder.HashDataForPay(receiver, "", token, amount, priorityFee);
            var evvmId = PayPayloadBuilder.EvvmId;
            var coreAddress = ContractAddresses.Core;
            var executor = ContractAddresses.ZeroAddress;
            var nonce = "10";
            var isAsyncExec = false;
            
            var message = $"{evvmId},{coreAddress},{hashPayload},{executor},{nonce},{isAsyncExec.ToString().ToLower()}";
            
            Debug.Log("=== Message with nonce 10 ===");
            Debug.Log($"Message: {message}");
        }
    }
}

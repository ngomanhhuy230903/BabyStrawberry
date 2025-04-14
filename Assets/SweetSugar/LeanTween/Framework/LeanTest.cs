// // ©2015 - 2025 Candy Smith
// // All rights reserved
// // Redistribution of this software is strictly not allowed.
// // Copy of this software can be obtained from unity asset store only.
// // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// // FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
// // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// // THE SOFTWARE.

using System.Collections;
using UnityEngine;

namespace SweetSugar.LeanTween.Framework
{
	public class LeanTester : MonoBehaviour {
		public float timeout = 15f;

		#if !UNITY_3_5 && !UNITY_4_0 && !UNITY_4_0_1 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_5
		public void Start(){
			StartCoroutine( timeoutCheck() );
		}

		IEnumerator timeoutCheck(){
			float pauseEndTime = Time.realtimeSinceStartup + timeout;
			while (Time.realtimeSinceStartup < pauseEndTime)
			{
				yield return 0;
			}
			if(LeanTest.testsFinished==false){
				Debug.Log(LeanTest.formatB("Tests timed out!"));
				LeanTest.overview();
			}
		}
		#endif
	}

	public class LeanTest : object {
		public static int expected = 0;
		private static int tests = 0;
		private static int passes = 0;

		public static float timeout = 15f;
		public static bool timeoutStarted = false;
		public static bool testsFinished = false;
	
		public static void debug( string name, bool didPass, string failExplaination = null){
			expect( didPass, name, failExplaination);
		}

		public static void expect( bool didPass, string definition, string failExplaination = null){
			float len = printOutLength(definition);
			int paddingLen = 40-(int)(len*1.05f);
			#if UNITY_FLASH
		string padding = padRight(paddingLen);
			#else
			string padding = "".PadRight(paddingLen,"_"[0]);
			#endif
			string logName = formatB(definition) +" " + padding + " [ "+ (didPass ? formatC("pass","green") : formatC("fail","red")) +" ]";
			if(didPass==false && failExplaination!=null)
				logName += " - " + failExplaination;
			Debug.Log(logName);
			if(didPass)
				passes++;
			tests++;
		
			// Debug.Log("tests:"+tests+" expected:"+expected);
			if(tests==expected && testsFinished==false){
				overview();
			}else if(tests>expected){
				Debug.Log(formatB("Too many tests for a final report!") + " set LeanTest.expected = "+tests);
			}

			if(timeoutStarted==false){
				timeoutStarted = true;
				GameObject tester = new GameObject();
				tester.name = "~LeanTest";
				LeanTester test = tester.AddComponent(typeof(LeanTester)) as LeanTester;
				test.timeout = timeout;
				#if !UNITY_EDITOR
			tester.hideFlags = HideFlags.HideAndDontSave;
				#endif
			}
		}
	
		public static string padRight(int len){
			string str = "";
			for(int i = 0; i < len; i++){
				str += "_";
			}
			return str;
		}
	
		public static float printOutLength( string str ){
			float len = 0.0f;
			for(int i = 0; i < str.Length; i++){
				if(str[i]=="I"[0]){
					len += 0.5f;
				}else if(str[i]=="J"[0]){
					len += 0.85f;
				}else{
					len += 1.0f;
				}
			}
			return len;
		}
	
		public static string formatBC( string str, string color ){
			return formatC(formatB(str),color);
		}
	
		public static string formatB( string str ){
			#if UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2
		return str;
			#else
			return "<b>"+ str + "</b>";
			#endif
		}
	
		public static string formatC( string str, string color ){
			#if UNITY_3_5 || UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2
		return str;
			#else
			return "<color="+color+">"+ str + "</color>";
			#endif
		}
	
		public static void overview(){ 
			testsFinished = true;
			int failedCnt = (expected-passes);
			string failedStr = failedCnt > 0 ? formatBC(""+failedCnt,"red") : ""+failedCnt;
			Debug.Log(formatB("Final Report:")+" _____________________ PASSED: "+formatBC(""+passes,"green")+" FAILED: "+failedStr+" ");
		}
	}
}
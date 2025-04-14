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

//namespace DentedPixel{

using System;
using UnityEngine;

namespace SweetSugar.LeanTween.Framework
{
	public class LTDescrOptional  {

		public Transform toTrans { get; set; }
		public Vector3 point { get; set; }
		public Vector3 axis { get; set; }
		public float lastVal{ get; set; }
		public Quaternion origRotation { get; set; }
		public LTBezierPath path { get; set; }
		public LTSpline spline { get; set; }
		public AnimationCurve animationCurve;
		public int initFrameCount;
		public Color color;

		public LTRect ltRect { get; set; } // maybe get rid of this eventually

		public Action<float> onUpdateFloat { get; set; }
		public Action<float,float> onUpdateFloatRatio { get; set; }
		public Action<float,object> onUpdateFloatObject { get; set; }
		public Action<Vector2> onUpdateVector2 { get; set; }
		public Action<Vector3> onUpdateVector3 { get; set; }
		public Action<Vector3,object> onUpdateVector3Object { get; set; }
		public Action<Color> onUpdateColor { get; set; }
		public Action<Color,object> onUpdateColorObject { get; set; }
		public Action onComplete { get; set; }
		public Action<object> onCompleteObject { get; set; }
		public object onCompleteParam { get; set; }
		public object onUpdateParam { get; set; }
		public Action onStart { get; set; }


//	#if !UNITY_3_5 && !UNITY_4_0 && !UNITY_4_0_1 && !UNITY_4_1 && !UNITY_4_2
//	public SpriteRenderer spriteRen { get; set; }
//	#endif
//
//	#if LEANTWEEN_1
//	public Hashtable optional;
//	#endif
//	#if !UNITY_3_5 && !UNITY_4_0 && !UNITY_4_0_1 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_5
//	public RectTransform rectTransform;
//	public UnityEngine.UI.Text uiText;
//	public UnityEngine.UI.Image uiImage;
//	public UnityEngine.Sprite[] sprites;
//	#endif


		public void reset(){
			animationCurve = null;

			this.onUpdateFloat = null;
			this.onUpdateFloatRatio = null;
			this.onUpdateVector2 = null;
			this.onUpdateVector3 = null;
			this.onUpdateFloatObject = null;
			this.onUpdateVector3Object = null;
			this.onUpdateColor = null;
			this.onComplete = null;
			this.onCompleteObject = null;
			this.onCompleteParam = null;
			this.onStart = null;

			this.point = Vector3.zero;
			this.initFrameCount = 0;
		}

		public void callOnUpdate( float val, float ratioPassed){
			if(this.onUpdateFloat!=null)
				this.onUpdateFloat(val);

			if (this.onUpdateFloatRatio != null){
				this.onUpdateFloatRatio(val,ratioPassed);
			}else if(this.onUpdateFloatObject!=null){
				this.onUpdateFloatObject(val, this.onUpdateParam);
			}else if(this.onUpdateVector3Object!=null){
				this.onUpdateVector3Object(LTDescr.newVect, this.onUpdateParam);
			}else if(this.onUpdateVector3!=null){
				this.onUpdateVector3(LTDescr.newVect);
			}else if(this.onUpdateVector2!=null){
				this.onUpdateVector2(new Vector2(LTDescr.newVect.x,LTDescr.newVect.y));
			}
		}
	}
}

//}

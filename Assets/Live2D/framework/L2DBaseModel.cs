/**
 *
 *  You can modify and use this source freely
 *  only for the development of application related Live2D.
 *
 *  (c) Live2D Inc. All rights reserved.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using live2d;

namespace live2d.framework
{
    public class L2DBaseModel
    {
        //  モデル関連
        protected ALive2DModel live2DModel = null;      // Live2Dモデルクラス
        protected L2DModelMatrix modelMatrix = null;        // Live2Dモデラー上の座標系からワールド座標系へ変換するための行列

        //  モーション・状態管理
        protected Dictionary<string, AMotion> expressions;  //  表情モーションデータ
        protected Dictionary<string, AMotion> motions;      //  モーションデータ

        protected L2DMotionManager mainMotionManager;       //  メインモーション
        protected L2DMotionManager expressionManager;       //  表情
        protected L2DEyeBlink eyeBlink;             //  自動目パチ
        protected L2DPhysics physics;               //  物理演算
        protected L2DPose pose;                 //  ポーズ。腕の切り替えなど。

        protected bool debugMode = false;
        protected bool initialized = false; //  初期化状態
        protected bool updating = false;        //  読み込み中ならtrue
        protected bool lipSync = false;     //  リップシンクが有効かどうか
        protected float lipSyncValue;           //  基本は0～1

        // 傾きの値。-1から1の範囲
        protected float accelX = 0;
        protected float accelY = 0;
        protected float accelZ = 0;

        // 向く方向の値。-1から1の範囲
        protected float dragX = 0;
        protected float dragY = 0;

        protected long startTimeMSec;


        public L2DBaseModel()
        {
            // モーションマネージャーを作成
            mainMotionManager = new L2DMotionManager();// MotionQueueManagerクラスからの継承なので、使い方は同一
            expressionManager = new L2DMotionManager();

            motions = new Dictionary<string, AMotion>();
            expressions = new Dictionary<string, AMotion>();
        }


        public L2DModelMatrix getModelMatrix()
        {
            return modelMatrix;
        }


        /*
         * 初期化されている場合はtrue。
         * 更新と描画可能になったときに初期化完了とみなす。
         *
         * @return
         */
        public bool isInitialized()
        {
            return initialized;
        }


        public void setInitialized(bool v)
        {
            initialized = v;
        }


        /*
         * モデルの読み込み中はtrue。
         * 更新と描画可能になったときに読み込み完了とみなす。
         *
         * @return
         */
        public bool isUpdating()
        {
            return updating;
        }


        public void setUpdating(bool v)
        {
            updating = v;
        }


        /*
         * Live2Dモデルクラスを取得する。
         * @return
         */
        public ALive2DModel getLive2DModel()
        {
            return live2DModel;
        }


        public void setLipSync(bool v)
        {
            lipSync = v;
        }


        public void setLipSyncValue(float v)
        {
            lipSyncValue = v;
        }


        public void setAccel(float x, float y, float z)
        {
            accelX = x;
            accelY = y;
            accelZ = z;
        }


        public void setDrag(float x, float y)
        {
            dragX = x;
            dragY = y;
        }


        public MotionQueueManager getMainMotionManager()
        {
            return mainMotionManager;
        }


        public MotionQueueManager getExpressionManager()
        {
            return expressionManager;
        }

        public void loadModelData(String path)
        {
            IPlatformManager pm = Live2DFramework.getPlatformManager();


            if (debugMode) pm.log("Load model : " + path);

            live2DModel = pm.loadLive2DModel(path);
            live2DModel.saveParam();

            if (Live2D.getError() != Live2D.L2D_NO_ERROR)
            {
                // 読み込み失敗
                pm.log("Error : Failed to loadModelData().");
                return;
            }

            var w = live2DModel.getCanvasWidth();
            var h = live2DModel.getCanvasHeight();
            modelMatrix = new L2DModelMatrix(w, h);

            if (w>h)
            {
                modelMatrix.setWidth(2);                
            }
            else
            {
                modelMatrix.setHeight(2);
            }

            modelMatrix.setCenterPosition(0, 0);
        }


        public void loadTexture(int no, String path)
        {
            IPlatformManager pm = Live2DFramework.getPlatformManager();
            if (debugMode) pm.log("Load Texture : " + path);

            pm.loadTexture(live2DModel, no, path);
        }

        public AMotion loadMotion(String name, String path)
        {
            IPlatformManager pm = Live2DFramework.getPlatformManager();
            if (debugMode) pm.log("Load Motion : " + path);


            Live2DMotion motion = null;


            byte[] buf = pm.loadBytes(path);
            motion = Live2DMotion.loadMotion(buf);

            if (name != null)
            {
                motions.Add(name, motion);
            }

            return motion;
        }

        public void loadExpression(String name, String path)
        {
            IPlatformManager pm = Live2DFramework.getPlatformManager();
            if (debugMode) pm.log("Load Expression : " + path);

            expressions.Add(name, L2DExpressionMotion.loadJson(pm.loadBytes(path)));
        }


        public void loadPose(String path)
        {
            IPlatformManager pm = Live2DFramework.getPlatformManager();
            if (debugMode) pm.log("Load Pose : " + path);
            pose = L2DPose.load(pm.loadBytes(path));
        }

        public void loadPhysics(String path)
        {
            IPlatformManager pm = Live2DFramework.getPlatformManager();
            if (debugMode) pm.log("Load Physics : " + path);
            physics = L2DPhysics.load(pm.loadBytes(path));
        }


        public bool getSimpleRect(String drawID,out float left,out float right,out float top,out float bottom)
        {
            int drawIndex = live2DModel.getDrawDataIndex(drawID);
            if (drawIndex < 0)
            {
                left = 0;
                right = 0;
                top = 0;
                bottom = 0;
                return false;// 存在しない場合
            }
            float[] points = live2DModel.getTransformedPoints(drawIndex);

            float l = live2DModel.getCanvasWidth();
            float r = 0;
            float t = live2DModel.getCanvasHeight();
            float b = 0;

            for (int j = 0; j < points.Length; j = j + 2)
            {
                float x = points[j];
                float y = points[j + 1];
                if (x < l) l = x;   //  最小のx
                if (x > r) r = x;   //  最大のx
                if (y < t) t = y;       //  最小のy
                if (y > b) b = y;//  最大のy
            }
            
            left=l;
            right=r;
            top=t;
            bottom=b;

            return true;
        }


        public bool hitTestSimple(String drawID, float testX, float testY)
        {
            float left =0;
            float right =0;
            float top =0;
            float bottom =0;

            if ( ! getSimpleRect(drawID, out left,out right,out top,out bottom))
            {
                return false;
            }

            float tx = modelMatrix.invertTransformX(testX);
            float ty = modelMatrix.invertTransformY(testY);

            return (left <= tx && tx <= right && top <= ty && ty <= bottom);
        }
    }
}
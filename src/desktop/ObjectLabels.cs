namespace Link360Desktop;
// COCO class order from the pinned OpenCV Zoo YOLOX demo (Apache-2.0).
// Source and model license are retained in models/ and whole-body-provenance.json.
internal static class ObjectLabels {
 static readonly string[] Names=["person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light","fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow","elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee","skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard","tennis racket","bottle","wine glass","cup","fork","knife","spoon","bowl","banana","apple","sandwich","orange","broccoli","carrot","hot dog","pizza","donut","cake","chair","couch","potted plant","bed","dining table","toilet","TV","laptop","mouse","remote","keyboard","cell phone","microwave","oven","toaster","sink","refrigerator","book","clock","vase","scissors","teddy bear","hair drier","toothbrush"];
 public static string Name(int id)=>id>=0&&id<Names.Length?Names[id]:"unknown object";
}

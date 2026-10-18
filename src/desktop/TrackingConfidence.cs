namespace Link360Desktop;
internal static class TrackingConfidence {
 static double minimum=.5;
 public static double Minimum {get=>minimum;set=>minimum=double.IsFinite(value)?Math.Clamp(value,.35,.8):.5;}
}

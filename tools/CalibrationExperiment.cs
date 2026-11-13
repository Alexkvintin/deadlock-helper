using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using DeadlockOverlay;
// Offline only. References the built application engine; never updates settings or downloads data.
public sealed class CalibrationInput {public PerformanceMatch[] Training=new PerformanceMatch[0],Validation=new PerformanceMatch[0];public bool VerifiedPatch;public Dictionary<string,double> IndependentScores=new Dictionary<string,double>();}
class CalibrationExperiment {
 static int Main(string[] args){if(args.Length!=1){Console.WriteLine("Usage: CalibrationExperiment.exe audited-dataset.json");return 2;}var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};var data=json.Deserialize<CalibrationInput>(File.ReadAllText(args[0]));if(!data.VerifiedPatch){Console.WriteLine("UNAVAILABLE: no verified patch provenance. MAE and stability unavailable.");return 0;}if(data.Training.Select(m=>m.Account).Intersect(data.Validation.Select(m=>m.Account)).Any())throw new InvalidDataException("Training/validation account overlap");var errors=new List<double>();int scored=0,missing=0;foreach(var group in data.Validation.GroupBy(m=>m.Account)){var r=PerformanceEngine.Evaluate(group.ToArray(),data.Training,false,true);if(!r.Score.HasValue){missing++;continue;}scored++;double reference;if(data.IndependentScores.TryGetValue(group.Key.ToString(),out reference))errors.Add(Math.Abs(r.Score.Value-reference));}Console.WriteLine("Experimental windows scored="+scored+" unavailable="+missing+" independently labeled="+errors.Count);Console.WriteLine("MAE="+(errors.Count>0?errors.Average().ToString("0.000"):"unavailable"));Console.WriteLine("Production LOW/HIGH remains disabled; bootstrap and blinded review are separate acceptance gates.");return 0;}
}

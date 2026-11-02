using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

[assembly: AssemblyVersion("1.0.0.1")]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context, System.Windows.Window window, ScriptEnvironment environment)
        {
            // TODO : Add here the code that is called when the script is launched from Eclipse.
            PlanSetup plan = context.PlanSetup;
            if(plan == null)
            {
                MessageBox.Show("No valid plan selected");
                return;
            }
            if (!plan.IsDoseValid)
            {
                MessageBox.Show("The plan selected has no valid dose.");
                return;
            }
            //LINQ query using FirstOrDefault to find the target based on the ID
            Structure target = plan.StructureSet.Structures.FirstOrDefault(st=>st.Id.Equals(plan.TargetVolumeID));
            if(target == null)
            {
                MessageBox.Show("Plan contains no target volume");
                return;
            }

            //set up plan metrics
            Dictionary<string, string> planMetrics = new Dictionary<string, string>();
            Dictionary<string, bool> toleranceChecks = new Dictionary<string, bool>();
            //add metrics to report.
            planMetrics.Add("Target", target.Id);
            planMetrics.Add("Target Volume", target.Volume.ToString("F1")+"cc");
            //add the number of Fractions.
            planMetrics.Add("Number of Fractions", plan.NumberOfFractions.ToString());
            //add the total Dose.
            planMetrics.Add("Total Dose", plan.TotalDose.ToString());
            //when a DoseValue is converted to a string, it displays the unit.
            //Max Dose
            //convert dose to absolute value.
            plan.DoseValuePresentation = DoseValuePresentation.Relative;
            DoseValue maxDosePercentDose = plan.Dose.DoseMax3D;
            string maxDosePercent = maxDosePercentDose.ToString();
            plan.DoseValuePresentation = DoseValuePresentation.Absolute;
            string maxDoseAbs = plan.Dose.DoseMax3D.ToString();
            planMetrics.Add("Max Dose", String.Format("{0} ({1})", maxDoseAbs, maxDosePercent));
            toleranceChecks.Add("Max Dose", maxDosePercentDose.Dose < 110.0);
            //MU Ratio (LINQ Expressions WHERE + SUM)
            double muSum = plan.Beams.Where(b=>!b.IsSetupField).Sum(b => b.Meterset.Value);
            double muRatio = muSum / plan.DosePerFraction.Dose;//cGy
            planMetrics.Add("MU Ratio", muRatio.ToString("F2"));
            toleranceChecks.Add("MU Ratio", muRatio <= 5.0);
            //Conformity Index. 
            //Find the body (to the V100 of the body) (hint: Look above at how we found the target);
            //Get V100 of the body using GetVolumeAtDose
            //Use the DICOM Type of EXTERNAL to find the body.
            Structure body = plan.StructureSet.Structures.FirstOrDefault(st => st.DicomType == "EXTERNAL");
            double v100Body = plan.GetVolumeAtDose(body, plan.TotalDose, VolumePresentation.AbsoluteCm3);
            //double v100Body = plan.GetVolumeAtDose(body, new DoseValue(100, DoseValue.DoseUnit.Percent), VolumePresentation.AbsoluteCm3);
            double ci = v100Body / target.Volume;
            planMetrics.Add("Conformity Index", ci.ToString("F2"));
            toleranceChecks.Add("Conformity Index", ci < 1.1);
            //Gradient Index V50/V100.
            double v50Body = plan.GetVolumeAtDose(body, plan.TotalDose*0.5, VolumePresentation.AbsoluteCm3);
            double gi = v50Body / v100Body;
            planMetrics.Add("Gradient Index", gi.ToString("F2"));
            toleranceChecks.Add("Gradient Index", gi < 2.5);
            //Homogeneity Index (D5-D95)/Dprescription. Doses on the target (use GetDoseAtVolume).
            double d5 = plan.GetDoseAtVolume(target, 5, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
            //can get numeric representaion of dose by calling .Dose at the end.
            double d95 = plan.GetDoseAtVolume(target, 95, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
            double hi = (d5 - d95) / plan.TotalDose.Dose;
            planMetrics.Add("Homogeneity Index", hi.ToString("F2"));


            window.Width = 450;
            window.Height = 600;
            window.Content = AddMetricsToView(planMetrics, toleranceChecks);
            
        }
        private FlowDocumentScrollViewer AddMetricsToView(Dictionary<string,string> parameters, Dictionary<string, bool> toleranceChecks)
        {
            FlowDocumentScrollViewer flowScroller = new FlowDocumentScrollViewer
            {
                Background = Brushes.White,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8)
            };

            FlowDocument flowDocument = new FlowDocument
            {
                FontFamily = new FontFamily("Calibri"),
                FontSize = 14,
                PagePadding = new Thickness(12),
                Background = Brushes.White
            };

            flowDocument.Blocks.Add(new Paragraph(new Run("Dosimetric Plan Metrics"))
            {
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 22,
                Margin = new Thickness(0, 0, 0, 14)
            });

            SolidColorBrush borderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x52, 0x88));
            SolidColorBrush headerBackground = new SolidColorBrush(Color.FromRgb(0xE9, 0xF1, 0xFA));
            SolidColorBrush alternateRowBackground = new SolidColorBrush(Color.FromRgb(0xF7, 0xFA, 0xFD));

            //add values to table.
            Table table = new Table();
            table.CellSpacing = 0;
            table.Columns.Add(new TableColumn() { Width = new GridLength(2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn() { Width = new GridLength(3, GridUnitType.Star) });
            table.RowGroups.Add(new TableRowGroup());
            table.RowGroups.First().Rows.Add(new TableRow());
            table.RowGroups.Last().Rows.Last().Cells.Add(new TableCell(new Paragraph(new Run("Property") { FontWeight = FontWeights.Bold }))
            {
                Padding = new Thickness(8, 6, 8, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = borderBrush,
                Background = headerBackground
            });
            table.RowGroups.Last().Rows.Last().Cells.Add(new TableCell(new Paragraph(new Run("Value") { FontWeight = FontWeights.Bold }))
            {
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = borderBrush,
                Background = headerBackground
            });

            int rowIndex = 0;
            foreach(var metric in parameters)
            {
                rowIndex++;
                table.RowGroups.First().Rows.Add(new TableRow());
                TableRow row = table.RowGroups.Last().Rows.Last();
                if (rowIndex % 2 == 0)
                {
                    row.Background = alternateRowBackground;
                }

                row.Cells.Add(new TableCell(new Paragraph(new Run(metric.Key)))
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    BorderThickness = new Thickness(1),
                    BorderBrush = borderBrush
                });

                Paragraph valueParagraph = new Paragraph() { TextAlignment = TextAlignment.Right };
                valueParagraph.Inlines.Add(new Run(metric.Value));

                if (toleranceChecks.ContainsKey(metric.Key))
                {
                    bool isPass = toleranceChecks[metric.Key];
                    Run statusIconRun = new Run(isPass ? "  ✔" : "  ✖")
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = isPass ? Brushes.ForestGreen : Brushes.Firebrick,
                        FontFamily = new FontFamily("Segoe UI Symbol")
                    };
                    valueParagraph.Inlines.Add(statusIconRun);
                }

                row.Cells.Add(new TableCell(valueParagraph)
                {
                    Padding = new Thickness(10, 6, 10, 6),
                    BorderThickness = new Thickness(1),
                    BorderBrush = borderBrush
                });
            }
            flowDocument.Blocks.Add(table);
            flowScroller.Document = flowDocument;
            return flowScroller;
        }
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Desktop;
public sealed class SubjectNameConverter : IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)=>values.Length==2 && values[0] is Guid id && values[1] is IEnumerable<Subject> subjects ? subjects.FirstOrDefault(x=>x.Id==id)?.Name??"Unknown subject" : "";
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>targetTypes.Select(_=>DependencyProperty.UnsetValue).ToArray();
}

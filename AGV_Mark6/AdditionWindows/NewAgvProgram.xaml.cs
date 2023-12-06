using AGV_Mark6.Model;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGV_Mark6.MainTaskManager_Methods;



namespace AGV_Mark6
{

    public partial class NewAgvProgram : Window
    {
        //В этом классе мы задаем параметры для АГВ через таблицу. Доехав до определенного шага, программа считывает с таблицы параметры. Понимает в каком состоянии на данном шаге и данной программе АГВ и решает что делать далее в классе MainTaskManager.

        private AGV_Storage_Context db= new AGV_Storage_Context();

        public NewAgvProgram()
        {

            InitializeComponent();

        }

        // Выгрузка таблицы Prog(n) из нашей БД
        public void TableChanged(int TableNumber)
        {
            
            if (TableNumber == 0) { TB_ProgramName.Text = ""; return; }
            switch (TableNumber)
            {
                case 1:
                    DG_Steps1.ItemsSource = db.Prog1.ToList();
                    break;                  
                case 2:                    
                    DG_Steps1.ItemsSource = db.Prog2.ToList();
                    break;                 
                case 3:                    
                    DG_Steps1.ItemsSource = db.Prog3.ToList();
                    break;                 
                case 4:                     
                    DG_Steps1.ItemsSource = db.Prog4.ToList();
                    break;                 
                case 5:                    
                    DG_Steps1.ItemsSource = db.Prog5.ToList();
                    break;                 
                case 6:                    
                    DG_Steps1.ItemsSource = db.Prog6.ToList();
                    break;                
                case 7:                    
                    DG_Steps1.ItemsSource = db.Prog7.ToList();
                    break;                 
                case 8:                    
                    DG_Steps1.ItemsSource = db.Prog8.ToList();
                    break;                  
                case 9:                    
                    DG_Steps1.ItemsSource = db.Prog9.ToList();
                    break;                 
                case 10:                    
                    DG_Steps1.ItemsSource = db.Prog10.ToList();
                    break;

                default:
                    break;
            }

        }

        //Очистка ячеек, при нажатии delete. 
        private void DG_Steps1_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.Space) { e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (DG_Steps1.SelectedCells.Count == 1)
                {
                    if (e.OriginalSource.GetType() == typeof(DataGridCell))
                    {
                        // Starts the Edit on the row;
                        DataGrid grd = (DataGrid)sender;
                        grd.BeginEdit(e);
                        
                    }
                }
                for (int i = 0; i < DG_Steps1.SelectedCells.Count; i++)
                {
                    try
                    {
                      
                            DataGridCellInfo cell = DG_Steps1.SelectedCells[i];
                        DG_Steps1.CurrentCell = cell;
                        DG_Steps1.BeginEdit();
                            ((TextBox)cell.Column.GetCellContent(cell.Item)).Text = "";
                        DG_Steps1.CommitEdit();

                        
                    }
                    catch 
                    {
                        continue;
                    }
                   
                }
               
                return;
            }
            
        }

        //ctrl+s для сохранения
        private void DG_Steps1_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //if (MainWindow.Programm_Changing_Marker == 0) { return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                DG_Steps1.CommitEdit();
                DG_Steps1.CommitEdit();
                db.SaveChanges();

            }
        } 
        
        //Обработка ввода номера программы. чтобы вводились только от 1 до 10 
        private void TB_ProgramName_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            MainWindow.OnlyNumbersAllowed(sender, e, TB_ProgramName, 10);
            try
            {

                

                if (int.Parse(TB_ProgramName.Text + e.Text) > 10)
                {

                    TB_ProgramName.Text = e.Text;

                }

            }
            catch (Exception)
            {

            }
        }


        //Маркер номера программы. Если проставляем то останавливается программа
        private void TB_ProgramName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            MainWindow.NoSpaces(TB_ProgramName);
            if (TB_ProgramName.Text == "") return;
            MainTaskManager.ProgramIsEditing = true;
            MainTaskManager.StopEventMethod();
            TB_ProgramName.CaretIndex = TB_ProgramName.Text.Length;
            TableChanged(int.Parse(TB_ProgramName.Text));

        }

        //Колонка в таблице. При изменении сохраняется
        private void CB_StartEvent_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

            DG_Steps1.CommitEdit();
            DG_Steps1.CommitEdit();
            db.SaveChanges();

        }

        //Сохранение при изменении ячейки таблицы
        private void DG_Steps1_CurrentCellChanged(object sender, EventArgs e)
        {
            if (e.ToString()==" ")
            {

            }
            DG_Steps1.CommitEdit();
            DG_Steps1.CommitEdit();
            db.SaveChanges();
        }

        //Кнопка сохранения. 
        private void BT_SaveProgram_Click(object sender, RoutedEventArgs e)
        {

            MainTaskManager.ProgramIsEditing = false;

            DG_Steps1.CommitEdit();
            DG_Steps1.CommitEdit();
            
            db.SaveChanges();
            TB_ProgramName.Text = "";

            this.Close();

        }

        bool TextChanged = false;
        //Ограничение ввода только цифры
        private void TB_TransitionMissCount_PreviewTextInput_1(object sender, TextCompositionEventArgs e)
        {
            int val;
            if (!Int32.TryParse(e.Text, out val)) //&& e.Text != "-")
            {
                e.Handled = true; // отклоняем ввод
                return;
            }
            TextChanged = true;
        }

        private void TB_TransitionMissCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextChanged)
            {
                db.SaveChanges();
                TextChanged = false;
            }

        }

        private void Window_Closed(object sender, EventArgs e)
        {

            MainTaskManager.ProgramIsEditing = false;
            MainWindow.AddProgramWindowOpened--;
            this.Close();

        }
        //Ограничиваем ввод. Только Цифры разрешены
        private void DG_Steps1_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DG_Steps1.CurrentCell.Column.Header.ToString() == "Перейти к Программе" | DG_Steps1.CurrentCell.Column.Header.ToString() == "Перейти к Шагу" | DG_Steps1.CurrentCell.Column.Header.ToString() == "Пропустить")
            {
                int val;
                if (!Int32.TryParse(e.Text, out val)) 
                {
                    e.Handled = true; // отклоняем ввод
                    return;
                }
            }
        }


        //блокнот, просто чтоб удобно было заполнять колонку Комментариев
        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            EX_NotePad.Width = 300;
            EX_NotePad.Height = 200;

        }
        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            EX_NotePad.Width = 25;
            EX_NotePad.Height = 25;
        }

    }
}

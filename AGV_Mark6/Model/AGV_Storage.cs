using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AGV_Mark6.Model
{
    //Здесь классы для взаимодействия с Базой данных
    public interface Prog
    {
        public int? Id { get; set; }
        public string? Step {

            get; set;

        }

        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? AdditionCommand { get; set; }
        public string? TransitionMissCount { get; set; }
        public string? LoadStatus { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        
        public string? Comments
        {
            get;set;
        }

    }

    //таблицы из Базы данных по другому не смог сделать( просто не знаю как.
    public class Prog1: Prog
    {

        
        public int? Id { get; set; }
        public string? Step
        {
            get; set;
        }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? AdditionCommand { get; set; }
        public string? TransitionMissCount { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? LoadStatus { get ; set ; }
       
        public string? Comments { get; set; }
      
    }
    public class Prog2 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? AdditionCommand { get; set; }
        public string? TransitionMissCount { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? LoadStatus { get ; set ; }
    
        public string? Comments
        {
            get;set;
        }
    }
    public class Prog3 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get; set; }
        public string? LoadStatus { get; set; }
        public string? TransitionMissCount { get; set; }
        public string? Comments { get; set; }

    }
    public class Prog4 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        public string? Comments{ get; set; }
    }
    public class Prog5 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        
        public string? Comments { get; set; }
    }
    public class Prog6 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        public string? Comments { get; set; }
    }
    public class Prog7 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        public string? Comments{ get; set; }
    }
    public class Prog8 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        public string? Comments{ get; set; }
    }
    public class Prog9 : Prog
    {
        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        public string? Comments { get; set; }
    }
    public class Prog10 : Prog
    {

        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? TransitionToStep { get; set; }
        public string? TransitionToProgram { get; set; }
        public string? StartEvent { get; set; }
        public string? Notification { get; set; }
        public string? Stop { get; set; }
        public string? AdditionCommand { get ; set ; }
        public string? LoadStatus { get ; set ; }
        public string? TransitionMissCount { get; set; }
        public string? Comments { get; set; }
    }

    //В этой таблице хранятся данные по точкам Home.
    public class additionCoordinates
    {
        public int? Id { get; set; }
        public string? Step { get; set; }
        public string? Program { get; set; }
        public string? PointName { get; set; }
        public string? Home { get; set; }
        public int InUse { get; set; }
    }

    //Таблица для отслеживания текущего состояния ловителей
    public class catchersCurrentStates
    {

        public int? Id { get; set; }
        public string? CatcherName { get; set; }
        public string? CatchersHome { get; set; }
        public int? State { get; set; }
        public int? CatcherMemoryState { get; set; }

    }

    //Таблица для хранения данных по изменению состояния АГВ для конкретных маршрутов
    public class catchersRoutes
    {

        public int? Id { get; set; }
        public string? CatcherName { get; set; }
        public string? CatcherRoute { get; set; }
        public string? CatchersHome { get; set; }
        public int? StateChange { get; set; }
        public int? Direction { get; set; }


    }

    //Таблица для добавления маршрутов
    public class RouteEditorStorage 
    {

        public int id { get; set; }
        public string? Program { get; set; }
        public string? Step { get; set; }
        public bool? LastOne { get; set; }

    }

    public class StatusList : List<string>
    {
        public StatusList()
        {
            this.Add("+");
            this.Add("-");
        }
    }

    public class HomeListFromDb : List<string>
    {
        public HomeListFromDb()
        {
            AGV_Storage_Context db = new AGV_Storage_Context();
            
            List<additionCoordinates> Homes = db.AdditionCoordinates.ToList();
            foreach(additionCoordinates item in Homes)
            {
                this.Add(item.PointName);
            }
            //добавляем пустой Home для полноты выбора
            this.Add(" ");
            
        }
    }

    public class LoadList : List<string>
    {
        public LoadList()
        {
            this.Add("Загружена");
            this.Add("С тележкой");
            this.Add("Пустая");
            this.Add(" ");
        }
    }

    //Класс регистров для ПО кнопок
    public class registersForButtons
    {

        public int? id { get; set; }
        public int RegisterNumber { get; set; }
        public string? CommandForRegister { get; set; }
        public int MechanicalButtonRegister { get; set; }
        

    }


    public class AGV_Storage_Context : DbContext
    {
        public DbSet<Prog1> Prog1 { get; set; }
        public DbSet<Prog2> Prog2 { get; set; }
        public DbSet<Prog3> Prog3 { get; set; }
        public DbSet<Prog4> Prog4 { get; set; }
        public DbSet<Prog5> Prog5 { get; set; }
        public DbSet<Prog6> Prog6 { get; set; }
        public DbSet<Prog7> Prog7 { get; set; }
        public DbSet<Prog8> Prog8 { get; set; }
        public DbSet<Prog9> Prog9 { get; set; }
        public DbSet<Prog10> Prog10 { get; set; }
        public DbSet<additionCoordinates> AdditionCoordinates { get; set; }
        public DbSet<registersForButtons> RegistersForButtons { get; set; }
        public DbSet<catchersCurrentStates> CatchersCurrentStates { get; set; }
        //База для хранения ловителей с маршрутами которые к ним относятся.
        public DbSet<catchersRoutes> CatchersRoutes { get; set; }

        public AGV_Storage_Context()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=AGV_ProgramsStorage_DB.db");
        }
    }


}

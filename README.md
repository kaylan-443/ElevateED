# ElevateED – School Management & Student Support System

ElevateED is a web-based school management and student support system designed to streamline academic and administrative processes within a secondary school environment.

The system brings together learner management, teacher management, attendance, assessments, academic support, communication and other school-related processes into a centralised platform.

## 🎯 Project Overview

Traditional school administration often requires multiple systems and manual processes to manage learners, teachers, attendance, academic records and communication.

ElevateED was developed to provide a centralised platform where different users can access functionality relevant to their roles while allowing school administrators to manage core school operations.

The project was developed as part of an ICT Application Development programme and demonstrates the use of object-oriented programming, MVC architecture, database-driven development, service-based application design and integration with external technologies.

## ✨ Key Features

### 👨‍💼 Administration

* Manage school-related information and users
* Manage learners and teachers
* Manage classes and subjects
* Manage announcements
* Manage extra classes
* Manage academic configurations
* Monitor school information through administrative functionality
* Access analytics and reporting functionality

### 👨‍🎓 Student Management

* Learner registration and application processes
* Student profiles
* Class and subject information
* Academic information
* Attendance records
* Homework and submissions
* Quizzes and assessments
* Study materials
* Extra-class participation

### 👨‍🏫 Teacher Management

* Teacher accounts and profiles
* Subject assignments
* Grade assignments
* Quiz management
* Academic content management
* Learner academic support

### 📋 Attendance Management

ElevateED includes an attendance management system for recording and managing learner attendance.

The project contains dedicated attendance functionality and services for managing attendance sessions and attendance records.

### 📝 Assessments & Academic Support

The system includes functionality supporting:

* Quizzes
* Quiz questions
* Homework
* Submissions
* Study materials
* Past papers
* Academic records
* Exam timetables
* Extra classes

### 🤖 AI-Powered Features

ElevateED incorporates AI-related functionality to provide additional academic support.

The project includes AI study functionality and services for working with AI-powered study features.

The service layer includes integrations such as:

* AI Study Service
* Gemini Service
* OpenAI Text-to-Speech Service
* AI Announcement Service
* PDF Extraction Service

### 📢 Communication

The system includes announcement and communication functionality to allow important information to be distributed through the platform.

Email-related functionality is also implemented through a dedicated email service.

## 🏗️ Architecture

ElevateED follows an ASP.NET MVC architecture with a separation between the application's presentation, business logic and data components.

The project is organised into several major areas:

```text
ElevateED/
│
├── App_Start/
├── Controllers/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── Content/
├── Scripts/
├── Migrations/
├── Uploads/
│
├── Global.asax
├── Web.config
└── ElevateED.csproj
```

### Controllers

Controllers handle requests and coordinate application functionality.

Examples include:

* `AdminController`
* `StudentController`
* `TeacherController`
* `AttendanceController`
* `ApplicationController`
* `AnalyticsController`
* `AIStudyController`
* `StudentQuizController`
* `TeacherQuizController`
* `AdminExtraClassController`

### Models

The Models layer represents the application's data and domain entities.

Examples include:

* `Student`
* `Teacher`
* `Class`
* `Subject`
* `Grade`
* `AttendanceRecord`
* `AttendanceSession`
* `Homework`
* `Submission`
* `QuizQuestion`
* `StudyMaterial`
* `ExamTimeTable`
* `Announcement`
* `ExtraClass`

### Services

The Services layer separates reusable business and integration logic from the controllers.

Examples include:

* Attendance Service
* AI Study Service
* Email Service
* Exam Timetable Service
* Gemini Service
* Podcast Service
* PDF Extraction Service
* OpenAI Text-to-Speech Service

## 🛠️ Technologies Used

* **C#**
* **ASP.NET MVC**
* **.NET Framework**
* **Entity Framework**
* **SQL Server**
* **HTML5**
* **CSS3**
* **JavaScript**
* **Bootstrap**
* **Azure Services**
* **AI API Integrations**
* **Git & GitHub**

## 🔐 Security & User Roles

The application includes account and role-based functionality to support different types of users.

The project contains an application user model and account controller for authentication-related functionality.

Different parts of the system are separated according to the responsibilities of administrators, teachers and students.

## 🗄️ Database

ElevateED uses a relational database structure to store and manage application data.

Entity Framework is used to work with the application's data model, with database migrations included in the project.

The data model contains relationships between key entities such as:

```text
Student
   │
   ├── Class
   ├── Subjects
   ├── Attendance
   ├── Homework
   ├── Quizzes
   ├── Submissions
   └── Extra Classes

Teacher
   │
   ├── Subjects
   ├── Grades
   ├── Quizzes
   └── Academic Content
```

## 🚀 Getting Started

### Prerequisites

Before running ElevateED, ensure you have:

* Visual Studio
* .NET Framework compatible with the project
* SQL Server / SQL Server Express
* Entity Framework dependencies
* Git

### Clone the Repository

```bash
git clone https://github.com/kaylan-443/ElevateED.git
```

Navigate into the project:

```bash
cd ElevateED
```

Open the solution:

```text
ElevateED.sln
```

Restore the required NuGet packages and build the solution using Visual Studio.

### Database Configuration

Configure the application's database connection in:

```text
Web.config
```

Run the Entity Framework migrations required by the project and ensure the configured SQL Server database is available.

### Run the Application

Open the solution in Visual Studio and run the application using IIS Express or the configured development server.

## 📁 Project Structure

| Folder        | Purpose                                            |
| ------------- | -------------------------------------------------- |
| `Controllers` | Handles application requests and user actions      |
| `Models`      | Application entities and database models           |
| `ViewModels`  | Data structures used between controllers and views |
| `Views`       | User interface and Razor views                     |
| `Services`    | Business logic and external service integrations   |
| `Migrations`  | Entity Framework database migrations               |
| `Scripts`     | JavaScript and client-side functionality           |
| `Content`     | CSS and other styling resources                    |
| `Uploads`     | Application upload storage                         |
| `App_Start`   | Application configuration and startup components   |

## 🎓 Project Purpose

ElevateED was created to demonstrate the practical application of software development concepts in a real-world school management scenario.

The project demonstrates experience with:

* Object-oriented programming
* MVC architecture
* Database-driven applications
* Entity Framework
* SQL database design
* Service-layer architecture
* Authentication and user management
* API integration
* AI integration
* Application debugging
* Software testing
* Git and GitHub development workflows

## 🔮 Future Improvements

Potential future improvements include:

* More advanced analytics and reporting
* Expanded AI-powered academic recommendations
* Improved mobile responsiveness
* Additional automated notifications
* More comprehensive role and permission management
* Deployment to a production cloud environment
* Automated testing and CI/CD integration

## 👨‍💻 Developer

**Kaylan Moonsamy**

ICT Application Development Student
Durban University of Technology

GitHub: https://github.com/kaylan-443

## 📄 License

This project was developed as an academic software development project.

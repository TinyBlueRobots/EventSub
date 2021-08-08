#!/bin/bash
if docker ps -a | grep sqlserver > /dev/null;
then
  docker start sqlserver
else
  docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=P@55w0rd" -p 1433:1433 -d --name sqlserver microsoft/mssql-server-linux
fi

if docker ps -a | grep postgres > /dev/null;
then
  docker start postgres
else
  docker run --name postgres -e POSTGRES_PASSWORD=password -p 5432:5432 -d postgres
fi

if docker ps -a | grep mysql > /dev/null;
then
  docker start mysql
else
  docker run --name mysql -e MYSQL_ROOT_PASSWORD=password -d -p 3306:3306 -p 33060:33060 mysql
fi